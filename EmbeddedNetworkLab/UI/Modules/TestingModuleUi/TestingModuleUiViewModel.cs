using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace EmbeddedNetworkLab.UI.Modules.TestingModuleUi
{
	partial class TestingModuleUiViewModel : ModuleViewModel
	{

		public override string Name => "Testing Module";

		// Exposed series for MVVM binding to the CartesianChart
		public ISeries[] Series { get; } = new ISeries[]
		{
			new LineSeries<double>
			{
				Values = new double[] { 3, 5, 2, 8, 6 },
				Fill = null
			}
		};


	}
}
