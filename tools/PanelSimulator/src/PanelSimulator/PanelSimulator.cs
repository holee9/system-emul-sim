using System;
using System.Collections.Generic;
using Common.Dto.Interfaces;
using Common.Dto.Dtos;
using PanelSimulator.Models;
using PanelSimulator.Models.Noise;
using PanelSimulator.Models.Physics;
using PanelSimulator.Models.Readout;
using PanelSimulator.Models.Temporal;
using PanelSimulator.Generators;

namespace PanelSimulator;

/// <summary>
/// X-ray Detector Panel Simulator.
/// Simulates pixel generation with noise and defects.
/// REQ-SIM-001: Implements ISimulator interface.
/// REQ-SIM-002: Configurable via detector_config.yaml.
/// REQ-SIM-010: Generate 2D pixel matrix with configurable resolution and bit depth.
/// REQ-PHY-001: Composite noise model (Poisson + Gaussian + FPN + 1/f).
/// REQ-PHY-003: Fixed Pattern Noise map (frame-invariant, seed-based).
/// REQ-PHY-006: Image lag model integration.
/// </summary>
public class PanelSimulator : ISimulator
{
    private PanelConfig? _config;
    private int _frameNumber;
    private bool _isInitialized;

    // Test pattern generators
    private readonly Dictionary<TestPattern, ITestPatternGenerator> _patternGenerators;

    // Physics enhancement models (initialized on first Initialize call)
    private FixedPatternNoiseMap? _fpnMap;
    private LagModel? _lagModel;

    /// <summary>
    /// Initializes a new instance of the PanelSimulator.
    /// </summary>
    public PanelSimulator()
    {
        _frameNumber = 0;
        _isInitialized = false;
        _patternGenerators = new Dictionary<TestPattern, ITestPatternGenerator>
        {
            { TestPattern.Counter, new CounterPatternGenerator() },
            { TestPattern.Checkerboard, new CheckerboardPatternGenerator() },
            { TestPattern.FlatField, new FlatFieldPatternGenerator() }
        };
    }

    /// <inheritdoc />
    public void Initialize(object config)
    {
        if (config is PanelConfig panelConfig)
        {
            _config = panelConfig;
            _frameNumber = 0;
            _isInitialized = true;

            // Initialize FPN map for Composite noise mode (REQ-PHY-003)
            _fpnMap = panelConfig.NoiseModel == NoiseModelType.Composite && panelConfig.FpnAmplitudePct > 0
                ? new FixedPatternNoiseMap(panelConfig.Rows, panelConfig.Cols,
                    panelConfig.FpnAmplitudePct / 100.0, panelConfig.Seed)
                : null;

            // Initialize LagModel when enabled (REQ-PHY-006)
            _lagModel = panelConfig.EnableImageLag && panelConfig.ImageLagFraction > 0
                ? new LagModel(new LagConfig(LagCoefficient: panelConfig.ImageLagFraction))
                : null;
        }
        else
        {
            throw new ArgumentException("Config must be of type PanelConfig.", nameof(config));
        }
    }

    /// <inheritdoc />
    public object Process(object input)
    {
        if (!_isInitialized || _config == null)
        {
            throw new InvalidOperationException("PanelSimulator is not initialized. Call Initialize first.");
        }

        ushort[] pixels;

        if (_config.TestPattern == TestPattern.PhysicsBased)
        {
            // Physics-based X-ray simulation: kVp/mAs → scintillator → gate → exposure
            var scintillator = new ScintillatorModel(new ScintillatorConfig(
                KVp: _config.KVp,
                MAs: _config.MAs));
            ushort[,] frame2D = scintillator.GenerateSignalFrame(_config.Rows, _config.Cols, _config.BitDepth);

            // Gate response with Vback/temperature-dependent dark current (REQ-PHY-004, REQ-PHY-005)
            var gateModel = new GateResponseModel(new GateResponseConfig(
                VbackVolts: _config.VbackVolts,
                TemperatureCelsius: _config.TemperatureCelsius));
            frame2D = gateModel.ApplyGateResponse(frame2D, gateOn: true, exposureTimeMs: _config.ExposureTimeMs);

            var exposureModel = new ExposureModel(new ExposureConfig(ExposureTimeMs: _config.ExposureTimeMs));
            frame2D = exposureModel.ApplyExposureScaling(frame2D);

            // REQ-PHY-001: Composite noise (Poisson shot + Gaussian readout + 1/f flicker)
            if (_config.NoiseModel == NoiseModelType.Composite)
            {
                // Full-well capacity scaled to bit depth for correct e⁻/DN conversion.
                // Reference: Varex PaxScan ~1525 e⁻/ADU (low gain) → ~100M e⁻ full-well (16-bit)
                double fullWellCapacity = _config.BitDepth == 14 ? 25_000_000.0 : 100_000_000.0;
                int adcBits = _config.BitDepth == 14 ? 14 : 16;

                var compositeConfig = new CompositeNoiseConfig(
                    EnablePoissonNoise: true,
                    EnableGaussianNoise: true,
                    EnableDarkCurrent: false,   // Dark offset already applied by GateResponseModel
                    EnableFlickerNoise: true,
                    ReadoutNoiseElectrons: _config.ReadoutNoiseElectrons,
                    FlickerNoiseAmplitude: 0.005,
                    NoiseFloorDN: 1.0,
                    FullWellCapacity: fullWellCapacity,
                    AdcBits: adcBits);
                var compositeGen = new CompositeNoiseGenerator(_config.Seed + _frameNumber, compositeConfig);
                frame2D = compositeGen.ApplyNoise(frame2D, _frameNumber);
            }
            else if (_config.NoiseModel == NoiseModelType.Gaussian && _config.NoiseStdDev > 0)
            {
                // Legacy Gaussian-only noise path
                var tempPixels = new ushort[_config.Rows * _config.Cols];
                for (int r = 0; r < _config.Rows; r++)
                    for (int c = 0; c < _config.Cols; c++)
                        tempPixels[r * _config.Cols + c] = frame2D[r, c];
                var noiseGenerator = new GaussianNoiseGenerator(_config.NoiseStdDev, _config.Seed + _frameNumber);
                tempPixels = noiseGenerator.ApplyNoise(tempPixels);
                for (int r = 0; r < _config.Rows; r++)
                    for (int c = 0; c < _config.Cols; c++)
                        frame2D[r, c] = tempPixels[r * _config.Cols + c];
            }

            // REQ-PHY-003: Apply Fixed Pattern Noise map (frame-invariant spatial structure)
            if (_fpnMap != null)
            {
                frame2D = _fpnMap.Apply(frame2D);
            }

            // REQ-PHY-006: Apply image lag (ghosting from previous frames)
            if (_lagModel != null)
            {
                frame2D = _lagModel.ApplyLag(frame2D);
            }

            // Flatten 2D to 1D array
            pixels = new ushort[_config.Rows * _config.Cols];
            for (int r = 0; r < _config.Rows; r++)
                for (int c = 0; c < _config.Cols; c++)
                    pixels[r * _config.Cols + c] = frame2D[r, c];

            if (_config.DefectRate > 0)
            {
                var defectMap = new DefectMap(_config.DefectRate, _config.Seed + _frameNumber);
                pixels = defectMap.ApplyDefects(pixels);
            }
        }
        else
        {
            // Generate base test pattern (Counter / Checkerboard / FlatField)
            pixels = _patternGenerators[_config.TestPattern].Generate(
                _config.Cols,
                _config.Rows,
                _config.BitDepth,
                _frameNumber);

            // REQ-SIM-013: Counter mode bypasses noise and defect injection
            if (_config.TestPattern != TestPattern.Counter)
            {
                // Apply noise model
                if (_config.NoiseModel == NoiseModelType.Gaussian && _config.NoiseStdDev > 0)
                {
                    var noiseGenerator = new GaussianNoiseGenerator(_config.NoiseStdDev, _config.Seed + _frameNumber);
                    pixels = noiseGenerator.ApplyNoise(pixels);
                }

                // Apply defects
                if (_config.DefectRate > 0)
                {
                    var defectMap = new DefectMap(_config.DefectRate, _config.Seed + _frameNumber);
                    pixels = defectMap.ApplyDefects(pixels);
                }
            }
        }

        // Create FrameData
        var frameData = new FrameData(_frameNumber, _config.Cols, _config.Rows, pixels);
        _frameNumber++;

        return frameData;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _frameNumber = 0;
        _lagModel?.Reset();
    }

    /// <inheritdoc />
    public string GetStatus()
    {
        if (!_isInitialized || _config == null)
        {
            return "PanelSimulator Status: Not Initialized";
        }

        return $"PanelSimulator Status: Ready | " +
               $"Resolution: {_config.Rows}x{_config.Cols} | " +
               $"Bit Depth: {_config.BitDepth} | " +
               $"Pattern: {_config.TestPattern} | " +
               $"Frame Number: {_frameNumber}";
    }
}
