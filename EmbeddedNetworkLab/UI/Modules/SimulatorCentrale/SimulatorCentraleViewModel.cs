using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmbeddedNetworkLab.Modules;
using System.Windows;

namespace EmbeddedNetworkLab.UI.Modules.SimulatorCentrale
{
	public partial class SimulatorCentraleViewModel : ModuleViewModel
	{
		public override string Name => "Simulator Centrale";

		[ObservableProperty]
		private string? statusText = "Prêt";

		public string StartStopLabel => IsRunning ? "Stop" : "Start";

		[RelayCommand(CanExecute = nameof(CanStart))]
		private void Start()
		{
			// Validate or prepare configuration here if needed

			if (!TryStart())
				return;

			StatusText = "En cours";
			// Démarrage du simulateur : ajouter la logique réelle ici si nécessaire.
		}

		private bool CanStart()
		{
			return !IsRunning;
		}

		[RelayCommand(CanExecute = nameof(CanStop))]
		private void Stop()
		{
			// Arrêter le simulateur proprement
			StopExecution();
			StatusText = "Arrêté";
		}

		private bool CanStop()
		{
			return IsRunning;
		}

		[RelayCommand(CanExecute = nameof(CanToggle))]
		private void Toggle()
		{
			if (IsRunning)
				Stop();
			else
				Start();
		}

		private bool CanToggle()
		{
			return IsRunning || CanStart();
		}

		protected override void OnRunningStateChanged(bool isRunning)
		{
			// Mettre à jour l'état des commandes et des propriétés liées au démarrage/arrêt
			StartCommand.NotifyCanExecuteChanged();
			StopCommand.NotifyCanExecuteChanged();
			ToggleCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(StartStopLabel));
			OnPropertyChanged(nameof(StatusText));
		}
	}
}