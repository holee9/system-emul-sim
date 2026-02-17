using System.Text.Json;
using Common.Dto.Dtos;
using FluentAssertions;
using Xunit;

namespace Common.Dto.Tests.Dtos;

/// <summary>
/// Tests for SpiTransaction DTO specification.
/// REQ-SIM-051: Common.Dto shall define data transfer objects including SpiTransaction.
/// </summary>
public class SpiTransactionTests
{
    [Fact]
    public void SpiTransaction_shall_be_immutable_record()
    {
        // Arrange & Act
        var transaction = new SpiTransaction(
            SpiCommand.Write,
            0x00u,
            new byte[] { 0x01, 0x02, 0x03 },
            new byte[] { 0x04, 0x05, 0x06 });

        // Assert
        transaction.Should().NotBeNull();
        transaction.GetType().IsClass.Should().BeTrue();
        transaction.GetType().IsValueType.Should().BeFalse();
        transaction.GetType().Name.Should().Be("SpiTransaction");
    }

    [Fact]
    public void SpiTransaction_should_have_required_properties()
    {
        // Arrange
        var expectedCommand = SpiCommand.Write;
        var expectedAddress = 0x1000u;
        var expectedWriteData = new byte[] { 0x01, 0x02, 0x03 };
        var expectedReadData = new byte[] { 0x04, 0x05, 0x06 };

        // Act
        var transaction = new SpiTransaction(
            expectedCommand,
            expectedAddress,
            expectedWriteData,
            expectedReadData);

        // Assert
        transaction.Command.Should().Be(expectedCommand);
        transaction.Address.Should().Be(expectedAddress);
        transaction.WriteData.Should().BeSameAs(expectedWriteData);
        transaction.ReadData.Should().BeSameAs(expectedReadData);
    }

    [Fact]
    public void SpiTransaction_should_allow_null_write_data_for_read_command()
    {
        // Arrange & Act
        var transaction = new SpiTransaction(
            SpiCommand.Read,
            0x1000u,
            null,
            new byte[] { 0x01, 0x02, 0x03 });

        // Assert
        transaction.Command.Should().Be(SpiCommand.Read);
        transaction.WriteData.Should().BeNull();
    }

    [Fact]
    public void SpiTransaction_should_allow_null_read_data_for_write_command()
    {
        // Arrange & Act
        var transaction = new SpiTransaction(
            SpiCommand.Write,
            0x1000u,
            new byte[] { 0x01, 0x02, 0x03 },
            null);

        // Assert
        transaction.Command.Should().Be(SpiCommand.Write);
        transaction.ReadData.Should().BeNull();
    }

    [Fact]
    public void SpiTransaction_should_validate_command_is_valid()
    {
        // Arrange
        var writeData = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var act = () => new SpiTransaction(
            (SpiCommand)99,
            0x1000u,
            writeData,
            null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("command");
    }

    [Fact]
    public void SpiTransaction_should_be_serializable_to_json()
    {
        // Arrange
        var transaction = new SpiTransaction(
            SpiCommand.Write,
            0x1000u,
            new byte[] { 0x01, 0x02, 0x03 },
            new byte[] { 0x04, 0x05, 0x06 });

        // Act
        var json = JsonSerializer.Serialize(transaction);
        var deserialized = JsonSerializer.Deserialize<SpiTransaction>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Command.Should().Be(transaction.Command);
        deserialized.Address.Should().Be(transaction.Address);
        deserialized.WriteData.Should().BeEquivalentTo(transaction.WriteData);
        deserialized.ReadData.Should().BeEquivalentTo(transaction.ReadData);
    }

    [Fact]
    public void SpiTransaction_should_override_ToString()
    {
        // Arrange
        var transaction = new SpiTransaction(
            SpiCommand.Write,
            0x1000u,
            new byte[] { 0x01, 0x02, 0x03 },
            new byte[] { 0x04, 0x05, 0x06 });

        // Act
        var result = transaction.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Write");
        result.Should().Contain("1000");
        result.Should().Contain("WriteDataLength");
        result.Should().Contain("ReadDataLength");
    }

    [Fact]
    public void SpiTransaction_should_implement_value_equality()
    {
        // Arrange
        var writeData = new byte[] { 0x01, 0x02, 0x03 };
        var readData = new byte[] { 0x04, 0x05, 0x06 };
        var tx1 = new SpiTransaction(SpiCommand.Write, 0x1000u, writeData, readData);
        var tx2 = new SpiTransaction(SpiCommand.Write, 0x1000u, writeData, readData);
        var tx3 = new SpiTransaction(SpiCommand.Read, 0x1000u, null, readData);

        // Act & Assert
        tx1.Should().Be(tx2);
        tx1.Should().NotBe(tx3);
        (tx1 == tx2).Should().BeTrue();
        (tx1 == tx3).Should().BeFalse();
    }

    [Fact]
    public void SpiTransaction_should_support_with_expression()
    {
        // Arrange
        var original = new SpiTransaction(
            SpiCommand.Write,
            0x1000u,
            new byte[] { 0x01, 0x02, 0x03 },
            new byte[] { 0x04, 0x05, 0x06 });

        // Act
        var modified = original with { Address = 0x2000u };

        // Assert
        modified.Address.Should().Be(0x2000u);
        modified.Command.Should().Be(original.Command);
        modified.WriteData.Should().BeSameAs(original.WriteData);
        modified.ReadData.Should().BeSameAs(original.ReadData);
    }
}
