using System.Collections.Generic;
using FmsSimulator.Models;
using FmsSimulator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FmsSimulator.Tests
{
    public class PlanGeneratorTests
    {
        private readonly PlanGenerator _planGenerator;

        public PlanGeneratorTests()
        {
            var config = new ConfigurationBuilder().Build();
            _planGenerator = new PlanGenerator(NullLogger<PlanGenerator>.Instance, config);
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
            var plans = _planGenerator.GeneratePlans(task, fleet);

            // ASSERT
            Assert.Single(plans);
            Assert.Equal("A1", plans[0].AssignedAmr.Id);
        }

        [Fact]
        public void GeneratePlans_ReturnsEmptyList_WhenAmrIsUnavailable()
        {
            // ARRANGE
            var fleet = new List<AmrState>
            {
                new() { Id = "A1", ModelName = "M1", PrimaryMission = "X", TopModuleType = "Lift", MaxPayloadKg = 500, LiftingHeightMm = 100, IsAvailable = false, CurrentPosition = (0, 0), BatteryLevel = 0.9 }
            };
            var task = new ProductionTask { TaskId = "T1", RequiredPayload = 400, RequiredModule = "Lift", RequiredLiftHeight = 90 };

            // ACT
            var plans = _planGenerator.GeneratePlans(task, fleet);

            // ASSERT
            Assert.Empty(plans);
        }

        [Fact]
        public void GeneratePlans_ReturnsEmptyList_WhenAmrLacksPayloadCapacity()
        {
            // ARRANGE
            var fleet = new List<AmrState>
            {
                new() { Id = "A1", ModelName = "M1", PrimaryMission = "X", TopModuleType = "Lift", MaxPayloadKg = 300, LiftingHeightMm = 100, IsAvailable = true, CurrentPosition = (0, 0), BatteryLevel = 0.9 }
            };
            var task = new ProductionTask { TaskId = "T1", RequiredPayload = 400, RequiredModule = "Lift", RequiredLiftHeight = 90 };

            // ACT
            var plans = _planGenerator.GeneratePlans(task, fleet);

            // ASSERT
            Assert.Empty(plans);
        }

        [Fact]
        public void GeneratePlans_ReturnsEmptyList_WhenAmrHasWrongModule()
        {
            // ARRANGE
            var fleet = new List<AmrState>
            {
                new() { Id = "A1", ModelName = "M1", PrimaryMission = "X", TopModuleType = "Arm", MaxPayloadKg = 500, LiftingHeightMm = 100, IsAvailable = true, CurrentPosition = (0, 0), BatteryLevel = 0.9 }
            };
            var task = new ProductionTask { TaskId = "T1", RequiredPayload = 400, RequiredModule = "Lift", RequiredLiftHeight = 90 };

            // ACT
            var plans = _planGenerator.GeneratePlans(task, fleet);

            // ASSERT
            Assert.Empty(plans);
        }

        [Fact]
        public void GeneratePlans_ReturnsEmptyList_WhenAmrLacksLiftingHeight()
        {
            // ARRANGE
            var fleet = new List<AmrState>
            {
                new() { Id = "A1", ModelName = "M1", PrimaryMission = "X", TopModuleType = "Lift", MaxPayloadKg = 500, LiftingHeightMm = 80, IsAvailable = true, CurrentPosition = (0, 0), BatteryLevel = 0.9 }
            };
            var task = new ProductionTask { TaskId = "T1", RequiredPayload = 400, RequiredModule = "Lift", RequiredLiftHeight = 90 };

            // ACT
            var plans = _planGenerator.GeneratePlans(task, fleet);

            // ASSERT
            Assert.Empty(plans);
        }
    }
}
