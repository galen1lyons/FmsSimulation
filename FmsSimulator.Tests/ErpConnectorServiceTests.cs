using Xunit;
using FmsSimulator.Services;
using FmsSimulator.Models;

namespace FmsSimulator.Tests;

public class ErpConnectorServiceTests
{
    private readonly ErpConnectorService _service;

    public ErpConnectorServiceTests()
    {
        _service = new ErpConnectorService();
    }

    [Fact]
    public void FetchAndTranslateOrders_ReturnsNonEmptyQueue()
    {
        // Act
        var result = _service.FetchAndTranslateOrders();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void FetchAndTranslateOrders_TranslatesOrdersCorrectly()
    {
        // Act
        var result = _service.FetchAndTranslateOrders();
        var firstTask = result.Peek();

        // Assert
        Assert.NotNull(firstTask);
        Assert.NotEmpty(firstTask.TaskId);
        Assert.NotEmpty(firstTask.FromLocation);
        Assert.NotEmpty(firstTask.ToLocation);
        Assert.True(firstTask.RequiredPayload > 0);
        Assert.NotEmpty(firstTask.RequiredModule);
    }

    [Fact]
    public void FetchAndTranslateOrders_HandlesUnknownSkus()
    {
        // Act
        var result = _service.FetchAndTranslateOrders();

        // Assert
        Assert.NotNull(result);  // Service should not throw exception for unknown SKUs
        
        // All tasks should have valid data
        foreach (var task in result)
        {
            Assert.NotEmpty(task.TaskId);
            Assert.NotEmpty(task.FromLocation);
            Assert.NotEmpty(task.ToLocation);
            Assert.True(task.RequiredPayload > 0);
            Assert.NotEmpty(task.RequiredModule);
            Assert.True(task.RequiredLiftHeight >= 0);
        }
    }
}