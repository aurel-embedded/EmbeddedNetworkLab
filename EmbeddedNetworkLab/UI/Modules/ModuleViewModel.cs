using CommunityToolkit.Mvvm.ComponentModel;

namespace EmbeddedNetworkLab.UI.Modules
{
	public abstract partial class ModuleViewModel : ObservableObject
	{
		public abstract string Name { get; }

		[ObservableProperty]
		private bool isRunning;

		protected bool TryStart()
		{
			if (IsRunning)
				return false;

			IsRunning = true;
			return true;
		}

		protected void StopExecution()
		{
			IsRunning = false;
		}

		// This method is called whenever the IsRunning property changes,
		// allowing derived classes to react to the change in running state.
		partial void OnIsRunningChanged(bool value)
		{
			OnRunningStateChanged(value);
		}

		// Allow derived classes to override this method to react to changes in the running state
		protected virtual void OnRunningStateChanged(bool isRunning)
		{
		}
	}
}
