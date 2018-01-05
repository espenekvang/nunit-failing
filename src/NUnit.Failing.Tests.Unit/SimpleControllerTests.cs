using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;
using FluentAssertions;
using NUnit.Failing.Controllers;
using NUnit.Framework;

namespace NUnit.Failing.Tests.Unit
{
    [TestFixture]
    public class SimpleControllerTests
    {
        [Test]
        public void TestIfAttributeExists()
        {
            var controllerActions = GetControllerActionMethodsWithoutAttribute(new string[0], typeof(MyAuthorizationAttribute));

            controllerActions.ToList().Should().HaveCount(0);
        }

        [Test]
        public async Task TestWithoutReflection()
        {
            var controller = new SimpleController();

            var result = (OkNegotiatedContentResult<string>) await controller.Get();
            result.Content.ShouldBeEquivalentTo("Hello world");
        }

        protected static IEnumerable<MethodInfo> GetControllerActionMethodsWithoutAttribute(string[] whitelist,
            params Type[] attributeTypes)
        {
            var allControllerTypes = GetSubtypeOf<ApiController>();

            var allVirtualMyBusinessControllerMethods =
                allControllerTypes.SelectMany(controller =>
                    controller.GetMethods()
                        .Where(m => m.IsNotBaseClassMethod() && m.IsPublic && !m.IsStatic && m.IsVirtual)).ToList();

            var controllerActionMethodsWithoutAttribute = allVirtualMyBusinessControllerMethods
                .Where(m =>
                    !m.HasOneOfTheAttributeTypes(attributeTypes)).ToList();
            return controllerActionMethodsWithoutAttribute;
        }

        private static ICollection<Type> GetSubtypeOf<TBaseType>()
        {
            var baseType = typeof(TBaseType);
            var baseDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            var assemblyFiles = baseDir.GetFiles("*.dll", SearchOption.AllDirectories);
            var subtypes = new List<Type>();
            foreach (var assemblyFile in assemblyFiles)
            {
                var assembly = Assembly.LoadFrom(assemblyFile.FullName);
                subtypes.AddRange(assembly.GetTypes().Where(type =>
                    baseType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract));
            }

            return subtypes;
        }
    }

    public static class MethodInfoExtensions
    {
        public static bool IsNotBaseClassMethod(this MethodInfo methodInfo)
        {
            if (methodInfo != null && methodInfo.DeclaringType != null)
            {
                return !methodInfo.DeclaringType.IsAssignableFrom(typeof(ApiController));
            }
            return false;
        }

        public static bool HasOneOfTheAttributeTypes(this MethodInfo methodInfo, Type[] attributeTypes)
        {
            return attributeTypes.Any(attributeType =>
                methodInfo.HasAttributeOfType(attributeType) ||
                methodInfo.DeclaringType.HasAttributeOfType(attributeType));
        }
    }

    public static class CustomAttributeProviderExtensions
    {
        public static bool HasAttributeOfType(this ICustomAttributeProvider member, Type type)
        {
            return GetAttributes(member, type).FirstOrDefault() != null;
        }

        public static object[] GetAttributes(this ICustomAttributeProvider member, Type type)
        {
            return member.GetCustomAttributes(type, false);
        }
    }
}
