using System.Collections.Generic;
using FmsSimulator.Models;
using FmsSimulator.Services;
using Xunit;

namespace FmsSimulator.Tests
{
    public class PlanGeneratorTests
    {
        private readonly OptimizedPlanGenerator _planGenerator;

        public PlanGeneratorTests()
        {
            _planGenerator = new OptimizedPlanGenerator();
        }

        [Fact]
        public void GeneratePlans_ReturnsPlan_WhenAmrIsSuitable()
        {
            // ARRANGE
            var fleet = new List<AmrState>
            {
                new() { Id = "A1", ModelName = "M1", PrimaryMission = "X", TopModuleType = "Lift", MaxPayloadKg = 500, LiftingHeightMm = 100, IsAvailable = true, CurrentPosition = (0, 0), BatteryLevel = 0.9 }
            };
            var task = new ProductionTask { TaskId = "T1", RequiredPayload = 400, RequiredModule = "Lift", RequiredLiftHeight = 90 };

            // ACT
            var result = _planGenerator.GeneratePlans(task, fleet);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Single(result.Data);
            Assert.Equal("A1", result.Data[0].AssignedAmr.Id);
        }

        [Fact]
        public void GeneratePlans_ReturnsFailure_WhenAmrIsUnavailable()
        {
            // ARRANGE
            var fleet = new List<AmrState>
            {
                new() { Id = "A1", ModelName = "M1", PrimaryMission = "X", TopModuleType = "Lift", MaxPayloadKg = 500, LiftingHeightMm = 100, IsAvailable = false, CurrentPosition = (0, 0), BatteryLevel = 0.9 }
            };
            var task = new ProductionTask { TaskId = "T1", RequiredPayload = 400, RequiredModule = "Lift", RequiredLiftHeight = 90 };

            // ACT
            var result = _planGenerator.GeneratePlans(task, fleet);

            // ASSERT
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void GeneratePlans_ReturnsFailure_WhenAmrLacksPayloadCapacity()
        {
            // ARRANGE
            var fleet = new List<AmrState>
            {
                new() { Id = "A1", ModelName = "M1", PrimaryMission = "X", TopModuleType = "Lift", MaxPayloadKg = 300, LiftingHeightMm = 100, IsAvailable = true, CurrentPosition = (0, 0), BatteryLevel = 0.9 }
            };
            var task = new ProductionTask { TaskId = "T1", RequiredPayload = 400, RequiredModule = "Lift", RequiredLiftHeight = 90 };

            // ACT
            var result = _planGenerator.GeneratePlans(task, fleet);

            // ASSERT
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void GeneratePlans_ReturnsFailure_WhenAmrHasWrongModule()
        {
            // ARRANGE
            var fleet = new List<AmrState>
            {
                new() { Id = "A1", ModelName = "M1", PrimaryMission = "X", TopModuleType = "Arm", MaxPayloadKg = 500, LiftingHeightMm = 100, IsAvailable = true, CurrentPosition = (0, 0), BatteryLevel = 0.9 }
            };
            var task = new ProductionTask { TaskId = "T1", RequiredPayload = 400, RequiredModule = "Lift", RequiredLiftHeight = 90 };

            // ACT
            var result = _planGenerator.GeneratePlans(task, fleet);

            // ASSERT
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void GeneratePlans_ReturnsFailure_WhenAmrLacksLiftingHeight()
        {
            // ARRANGE
            var fleet = new List<AmrState>
            {
                new() { Id = "A1", ModelName = "M1", PrimaryMission = "X", TopModuleType = "Electric AGV Lift", MaxPayloadKg = 500, LiftingHeightMm = 80, IsAvailable = true, CurrentPosition = (0, 0), BatteryLevel = 0.9 }
            };
            var task = new ProductionTask { TaskId = "T1", RequiredPayload = 400, RequiredModule = "Electric AGV Lift", RequiredLiftHeight = 90 };

            // ACT
            var result = _planGenerator.GeneratePlans(task, fleet);

            // ASSERT
            Assert.False(result.IsSuccess);
        }
    }
}
