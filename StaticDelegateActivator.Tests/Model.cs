using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StaticDelegateActivator.Tests.Model
{
	public delegate IEnumerable<int> DGetIntegers();
	public delegate IEnumerable<string> DGetStrings();

	public static class IntegerSources
	{
		public static IEnumerable<int> GetOneToTen()
		{
			return Enumerable.Range(1, 10);
		}

		public static DGetIntegers GetOnePlusEach(DGetIntegers input)
		{
			return () => input().Select(i => i + 1);
		}
	}

	public static class StringSources
	{
		public static DGetStrings GetIntegersAsStrings(DGetIntegers input)
		{
			return () => input().Select(i => i.ToString());
		}
	}

	public interface SomeInterface
	{

	}

	public class SomeImplementation : SomeInterface
	{
		public SomeImplementation(SomeInterface inner) { }
	}

	public class SomeOtherImplementation : SomeInterface
	{
		public SomeOtherImplementation(SomeInterface inner) { }
	}
}
