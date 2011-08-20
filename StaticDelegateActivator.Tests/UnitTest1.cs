using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Castle.Windsor;
using StaticDelegateActivator.Tests.Model;
using Castle.MicroKernel.Registration;

namespace StaticDelegateActivator.Tests
{
	[TestClass]
	public class UnitTest1
	{
		[TestMethod]
		public void TestMethod1()
		{
			var container = new WindsorContainer();

			container.Register(
				Component.For<DGetIntegers>().Instance(IntegerSources.GetOneToTen),
				Component.For<DGetStrings>().ImplementedBy(typeof(StringSources)).Named("GetIntegersAsStrings").Activator<StaticDelegateActivator>()
				);

			var getStrings = container.Resolve<DGetStrings>();

			Assert.IsTrue(Enumerable.SequenceEqual(new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" }, getStrings()));

			container.Release(getStrings);
		}
	}
}
