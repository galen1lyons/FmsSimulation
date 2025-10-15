using System.Collections.Generic;
using FmsSimulator.Models;
using FmsSimulator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FmsSimulator.Tests
{
    public class SimpleMcdmEngineTests
    {
        private IConfiguration _configuration;

        public SimpleMcdmEngineTests()
        {
            // Set up a consistent configuration for all tests in this class
            var inMemorySettings = new Dictionary<string, string> {
                {"FmsSettings:TargetPosition:X", "10"},
                {"FmsSettings:TargetPosition:Y", "10"}
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }

        [Fact]
        public void SelectBestPlan_PrefersHigherBattery_UnitTest()
        {
            // ARRANGE
            var amr1 = new AmrState { Id = "A1", ModelName = "M1", PrimaryMission = "X", TopModuleType = "Lift", MaxPayloadKg = 500, IsAvailable = true, CurrentPosition = (0, 0), BatteryLevel = 0.2 };
            var amr2 = new AmrState { Id = "A2", ModelName = "M2", PrimaryMission = "X", TopModuleType = "Lift", MaxPayloadKg = 500, IsAvailable = true, CurrentPosition = (1, 1), BatteryLevel = 0.9 };
            var task = new ProductionTask { TaskId = "T1", RequiredPayload = 100, RequiredModule = "Lift" };
            var plans = new List<AssignmentPlan>
            {
                new() { AssignedAmr = amr1, Task = task },
                new() { AssignedAmr = amr2, Task = task }
            };

            var engine = new SimpleMcdmEngine(NullLogger<SimpleMcdmEngine>.Instance, _configuration);

            // ACT
            var best = engine.SelectBestPlan(plans);

            // ASSERT
            Assert.NotNull(best);
            Assert.Equal("A2", best.AssignedAmr.Id);
        }

        [Fact]
        public void SelectBestPlan_PrefersHigherBattery_IntegrationTest()
        {
            // ARRANGE
            var amr1 = new AmrState { Id = "A1", ModelName = "M1", PrimaryMission = "X", TopModuleType = "Lift", MaxPayloadKg = 500, IsAvailable = true, CurrentPosition = (0, 0), BatteryLevel = 0.2 };
            var amr2 = new AmrState { Id = "A2", ModelName = "M2", PrimaryMission = "X", TopModuleType = "Lift", MaxPayloadKg = 500, IsAvailable = true, CurrentPosition = (1, 1), BatteryLevel = 0.9 };
            var task = new ProductionTask { TaskId = "T1", RequiredPayload = 100, RequiredModule = "Lift" };
            var plans = new List<AssignmentPlan>
            {
                new() { AssignedAmr = amr1, Task = task },
                new() { AssignedAmr = amr2, Task = task }
            };

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(_configuration);
            services.AddLogging();
            services.AddSingleton<IMcdmEngine, SimpleMcdmEngine>();
            using var provider = services.BuildServiceProvider();
            var engine = provider.GetRequiredService<IMcdmEngine>();

            // ACT
            var best = engine.SelectBestPlan(plans);

            // ASSERT
            Assert.NotNull(best);
            Assert.Equal("A2", best.AssignedAmr.Id);
        }
    }
}
