﻿using MediatR.Extensions.Microsoft.DependencyInjection.Tests;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using System.Reflection.PortableExecutable;
using MediatR.Tests.MicrosoftExtensionsDI;

namespace MediatR.Tests
{
    public class GenericRequestHandlerTests : BaseGenericRequestHandlerTests
    {

        [Theory]
        [InlineData(9, 3, 3)]
        [InlineData(10, 4, 4)]
        [InlineData(1, 1, 1)]
        [InlineData(50, 3, 3)]
        public void ShouldResolveAllCombinationsOfGenericHandler(int numberOfClasses, int numberOfInterfaces, int numberOfTypeParameters)
        {
            var services = new ServiceCollection();

            var dynamicAssembly = GenerateCombinationsTestAssembly(numberOfClasses, numberOfInterfaces, numberOfTypeParameters);

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblies(dynamicAssembly);
            });

            var provider = services.BuildServiceProvider();

            var dynamicRequestType = dynamicAssembly.GetType("DynamicRequest")!;

            int expectedCombinations = CalculateTotalCombinations(numberOfClasses, numberOfInterfaces, numberOfTypeParameters);

            var testClasses = Enumerable.Range(1, numberOfClasses)
                .Select(i => dynamicAssembly.GetType($"TestClass{i}")!)
                .ToArray();

            var combinations = GenerateCombinations(testClasses, numberOfInterfaces);          

            foreach (var combination in combinations)
            {
                var concreteRequestType = dynamicRequestType.MakeGenericType(combination);
                var requestHandlerInterface = typeof(IRequestHandler<>).MakeGenericType(concreteRequestType);

                var handler = provider.GetService(requestHandlerInterface);
                handler.ShouldNotBeNull($"Handler for {concreteRequestType} should not be null");
            }            
        }

        [Theory]
        [InlineData(9, 3, 3)]
        [InlineData(10, 4, 4)]
        [InlineData(1, 1, 1)]
        [InlineData(50, 3, 3)]
        public void ShouldRegisterTheCorrectAmountOfHandlers(int numberOfClasses, int numberOfInterfaces, int numberOfTypeParameters)
        {  
            var dynamicAssembly = GenerateCombinationsTestAssembly(numberOfClasses, numberOfInterfaces, numberOfTypeParameters);          
            int expectedCombinations = CalculateTotalCombinations(numberOfClasses, numberOfInterfaces, numberOfTypeParameters);
            var testClasses = Enumerable.Range(1, numberOfClasses)
               .Select(i => dynamicAssembly.GetType($"TestClass{i}")!)
               .ToArray();
            var combinations = GenerateCombinations(testClasses, numberOfInterfaces);
            combinations.Count.ShouldBe(expectedCombinations, $"Should have tested all {expectedCombinations} combinations");
        }

        [Theory]
        [InlineData(9, 3, 3)]
        [InlineData(10, 4, 4)]
        [InlineData(1, 1, 1)]
        [InlineData(50, 3, 3)]
        public void ShouldNotRegisterDuplicateHandlers(int numberOfClasses, int numberOfInterfaces, int numberOfTypeParameters)
        {
            var dynamicAssembly = GenerateCombinationsTestAssembly(numberOfClasses, numberOfInterfaces, numberOfTypeParameters);
            int expectedCombinations = CalculateTotalCombinations(numberOfClasses, numberOfInterfaces, numberOfTypeParameters);
            var testClasses = Enumerable.Range(1, numberOfClasses)
               .Select(i => dynamicAssembly.GetType($"TestClass{i}")!)
               .ToArray();
            var combinations = GenerateCombinations(testClasses, numberOfInterfaces);
            var hasDuplicates = combinations
              .Select(x => string.Join(", ", x.Select(y => y.Name)))
              .GroupBy(x => x)
              .Any(g => g.Count() > 1);

            hasDuplicates.ShouldBeFalse();
        }

        [Fact]
        public void ShouldThrowExceptionWhenRegisterningHandlersWithNoConstraints()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(new Logger());

            var assembly = GenerateMissingConstraintsAssembly();

            Should.Throw<ArgumentException>(() =>
            {
                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(assembly);
                });
            })
            .Message.ShouldContain("When registering generic requests with more than two type parameters, each type parameter must have at least one constraint of type interface or class.");
        }

        [Fact]
        public void ShouldThrowExceptionWhenTypesClosingExceedsMaximum()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(new Logger());

            var assembly = GenerateTypesClosingExceedsMaximumAssembly();

            Should.Throw<ArgumentException>(() =>
            {
                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(assembly);
                });
            })
            .Message.ShouldContain("One of the generic type parameter's count of types that can close exceeds the maximum length allowed");
        }

        [Fact]
        public void ShouldThrowExceptionWhenGenericHandlerRegistrationsExceedsMaximum()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(new Logger());

            var assembly = GenerateHandlerRegistrationsExceedsMaximumAssembly();

            Should.Throw<ArgumentException>(() =>
            {
                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(assembly);
                });
            })
            .Message.ShouldContain("The total number of generic type registrations exceeds the maximum allowed");
        }

        [Fact]
        public void ShouldThrowExceptionWhenGenericTypeParametersExceedsMaximum()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(new Logger());

            var assembly = GenerateGenericTypeParametersExceedsMaximumAssembly();

            Should.Throw<ArgumentException>(() =>
            {
                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(assembly);
                });
            })
            .Message.ShouldContain("The number of generic type parameters exceeds the maximum allowed");
        }

        [Fact]
        public void ShouldThrowExceptionWhenTimeoutOccurs()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(new Logger());

            var assembly = GenerateTimeoutOccursAssembly();

            Should.Throw<TimeoutException>(() =>
            {
                services.AddMediatR(cfg =>
                {
                    cfg.MaxGenericTypeParameters = 0;
                    cfg.MaxGenericTypeRegistrations = 0;
                    cfg.MaxTypesClosing = 0;
                    cfg.RegistrationTimeout = 1000;
                    cfg.RegisterServicesFromAssembly(assembly);
                });
            })
            .Message.ShouldBe("The generic handler registration process timed out.");
        }

        [Fact]
        public void ShouldNotRegisterGenericHandlersWhenOptingOut()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(new Logger());

            var assembly = GenerateOptOutAssembly();
            services.AddMediatR(cfg =>
            {
                //opt out flag set
                cfg.RegisterGenericHandlers = false;
                cfg.RegisterServicesFromAssembly(assembly);
            });

            var provider = services.BuildServiceProvider();
            var testClasses = Enumerable.Range(1, 2)
                .Select(i => assembly.GetType($"TestClass{i}")!)
                .ToArray();
            var requestType = assembly.GetType("OptOutRequest")!;
            var combinations = GenerateCombinations(testClasses, 2);

            var concreteRequestType = requestType.MakeGenericType(combinations.First());
            var requestHandlerInterface = typeof(IRequestHandler<>).MakeGenericType(concreteRequestType);

            var handler = provider.GetService(requestHandlerInterface);
            handler.ShouldBeNull($"Handler for {concreteRequestType} should be null");


        }
    }
}
