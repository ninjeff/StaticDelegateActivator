using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Castle.MicroKernel.ComponentActivator;
using Castle.Core;
using Castle.Windsor;
using Castle.MicroKernel;
using Castle.MicroKernel.Context;
using Castle.Core.Internal;
using System.Reflection;

namespace StaticDelegateActivator
{
	public class StaticDelegateActivator : AbstractComponentActivator
	{
		public StaticDelegateActivator(ComponentModel model, IKernel kernel, ComponentInstanceDelegate onCreation, ComponentInstanceDelegate onDestruction)
			: base(model, kernel, onCreation, onDestruction) { }

		protected override object InternalCreate(CreationContext context)
		{
			var instance = this.Instantiate(context);
			context.AddContextualProperty(this, instance);
			return instance;
		}

		protected virtual object Instantiate(CreationContext context)
		{
			var declaringType = this.Model.Implementation;
			var method = declaringType.GetMethod(this.Model.Name);

			Type[] signature;
			var arguments = this.CreateStaticMethodArguments(method, context, out signature);
			return this.CallMethod(context, method, arguments, signature);
		}

		protected virtual object CallMethod(CreationContext context, System.Reflection.MethodInfo method, object[] arguments, Type[] signature)
		{
			try
			{
				return method.Invoke(null, arguments);
			}
			catch (Exception ex)
			{
				if (arguments != null)
				{
					foreach (var argument in arguments)
					{
						this.Kernel.ReleaseComponent(argument);
					}
				}
				throw new ComponentActivatorException(string.Format("ComponentActivator: could not call method {0} of type {1}.", this.Model.Name, this.Model.Implementation.FullName), ex);
			}
		}

		protected virtual object[] CreateStaticMethodArguments(System.Reflection.MethodInfo method, CreationContext context, out Type[] signature)
		{
			signature = null;

			if (method == null)
			{
				return null;
			}

			var arguments = new object[method.GetParameters().Length];
			if (arguments.Length == 0)
			{
				return null;
			}

			try
			{
				signature = new Type[arguments.Length];
				this.CreateStaticMethodArgumentsCore(method, arguments, context, signature);
			}
			catch
			{
				foreach (var argument in arguments)
				{
					if (argument == null)
					{
						break;
					}
					Kernel.ReleaseComponent(argument);
				}
				throw;
			}

			return arguments;
		}

		private void CreateStaticMethodArgumentsCore(System.Reflection.MethodInfo method, object[] arguments, CreationContext context, Type[] signature)
		{
			var index = 0;
			foreach (var parameter in method.GetParameters())
			{
				var dependency = new DependencyModel(DependencyType.Service, parameter.Name, parameter.ParameterType, parameter.IsOptional, parameter.HasDefaultValue(), parameter.DefaultValue);
				object value;
				using (new DependencyTrackingScope(context, this.Model, method, dependency))
				{
					value = this.Kernel.Resolver.Resolve(context, context.Handler, this.Model, dependency);
				}
				arguments[index] = value;
				signature[index++] = dependency.TargetType;
			}
		}

		protected override void InternalDestroy(object instance)
		{
			// Don't do anything
		}
	}

	internal class DependencyTrackingScope : IDisposable
	{
		private readonly DependencyModel dependencyTrackingKey;
		private readonly DependencyModelCollection dependencies;

		public DependencyTrackingScope(CreationContext creationContext, ComponentModel model, MemberInfo info, DependencyModel dependencyModel)
		{
			if (dependencyModel.TargetItemType == typeof(IKernel))
			{
				return;
			}

			dependencies = creationContext.Dependencies;

			// We track dependencies in order to detect cycled graphs
			// This prevents a stack overflow
			dependencyTrackingKey = TrackDependency(model, info, dependencyModel);
		}

		public void Dispose()
		{
			// if the dependency were being tracked, and we reached the dispose...
			if (dependencies != null && dependencyTrackingKey != null)
			{
				// ...then the dependency was resolved successfully, we can stop tracking it.
				UntrackDependency(dependencyTrackingKey);
			}
		}

		private DependencyModelExtended TrackDependency(ComponentModel model, MemberInfo info, DependencyModel dependencyModel)
		{
			var trackingKey = new DependencyModelExtended(model, dependencyModel, info);

			if (dependencies.Contains(trackingKey))
			{
				var message = new StringBuilder("A cycle was detected when trying to resolve a dependency. ");
				message.Append("The dependency graph that resulted in a cycle is:");

				foreach (var key in dependencies)
				{
					var extendedInfo = key as DependencyModelExtended;
					if (extendedInfo != null)
					{
						message.AppendLine();
						message.AppendFormat(" - {0} for {1} in type {2}",
						key, extendedInfo.Info, extendedInfo.Info.DeclaringType);
					}
					else
					{
						message.AppendLine();
						message.AppendFormat(" - {0}", key);
					}
				}

				message.AppendLine();
				message.AppendFormat(" + {0} for {1} in {2}",
				dependencyModel, info, info.DeclaringType);
				message.AppendLine();

				throw new CircularDependencyException(message.ToString());
			}

			dependencies.Add(trackingKey);

			return trackingKey;
		}

		private void UntrackDependency(DependencyModel model)
		{
			dependencies.Remove(model);
		}

		#region DependencyModelExtended

		/// <summary>
		/// Extends <see cref="DependencyModel"/> adding <see cref="MemberInfo"/> and <see cref="ComponentModel"/>
		/// information. The MemberInfo is only useful to provide detailed information
		/// on exceptions.
		/// The ComponentModel is required so we can get resolve an object that takes as a parameter itself, but
		/// with difference model. (See IoC 51 for the details)
		/// </summary>
#if (!SILVERLIGHT)
		[Serializable]
#endif
		internal class DependencyModelExtended : DependencyModel
		{
			private readonly ComponentModel model;
			private readonly MemberInfo info;

			public DependencyModelExtended(ComponentModel model, DependencyModel inner, MemberInfo info)
				:
				base(inner.DependencyType, inner.DependencyKey, inner.TargetType, inner.IsOptional)
			{
				this.model = model;
				this.info = info;
			}

			public MemberInfo Info
			{
				get { return info; }
			}

			public override bool Equals(object obj)
			{
				var other = obj as DependencyModelExtended;
				if (other == null)
				{
					return false;
				}
				return other.Info == Info &&
				other.model == model &&
				base.Equals(other);
			}

			public override int GetHashCode()
			{
				var infoHash = 37 ^ Info.GetHashCode();
				return base.GetHashCode() + infoHash;
			}
		}

		#endregion
	}
}
