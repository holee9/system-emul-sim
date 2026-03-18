using System;
using System.Collections.Generic;
using Common.Dto.Interfaces;
using Common.Dto.Dtos;
using PanelSimulator.Models;
using PanelSimulator.Models.Physics;
using PanelSimulator.Models.Readout;
using PanelSimulator.Generators;

namespace PanelSimulator;

/// <summary>
/// X-ray Detector Panel Simulator.
/// Simulates pixel generation with noise and defects.
/// REQ-SIM-001: Implements ISimulator interface.
/// REQ-SIM-002: Configurable via detector_config.yaml.
/// REQ-SIM-010: Generate 2D pixel matrix with configurable resolution and bit depth.
/// </summary>
public class PanelSimulator : ISimulator
{
    private PanelConfig? _config;
    private int _frameNumber;
    private bool _isInitialized;

    // Test pattern generators
    private readonly Dictionary<TestPattern, ITestPatternGenerator> _patternGenerators;

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

            var gateModel = new GateResponseModel(new GateResponseConfig());
            frame2D = gateModel.ApplyGateResponse(frame2D, gateOn: true, exposureTimeMs: _config.ExposureTimeMs);

            var exposureModel = new ExposureModel(new ExposureConfig(ExposureTimeMs: _config.ExposureTimeMs));
            frame2D = exposureModel.ApplyExposureScaling(frame2D);

            // Flatten 2D to 1D array
            pixels = new ushort[_config.Rows * _config.Cols];
            for (int r = 0; r < _config.Rows; r++)
                for (int c = 0; c < _config.Cols; c++)
                    pixels[r * _config.Cols + c] = frame2D[r, c];

            // Apply noise and defects to physics frame
            if (_config.NoiseModel == NoiseModelType.Gaussian && _config.NoiseStdDev > 0)
            {
                var noiseGenerator = new GaussianNoiseGenerator(_config.NoiseStdDev, _config.Seed + _frameNumber);
                pixels = noiseGenerator.ApplyNoise(pixels);
            }

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
