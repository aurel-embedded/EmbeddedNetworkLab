using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EmbeddedNetworkLab.UI.Modules.Tests.LiveCharts2
{
	partial class LiveCharts2ViewModel : ModuleViewModel
	{

		public override string Name => "LiveCharts2";

		const int MaxPoints = 200;
		public ObservableCollection<ObservablePoint> Points { get; } = new();

		public ISeries[] Series { get; }
		public Axis[] XAxes { get; }
		public Axis[] YAxes { get; }

		private readonly DispatcherTimer _timer;
		private double _t;
		private double _x;

		public LiveCharts2ViewModel()
		{
			Series =
			[
				new LineSeries<ObservablePoint>
				{
					Values = Points,
					Fill = null,
					GeometrySize = 0,
					LineSmoothness = 0
				}
			];

			XAxes =
			[
				new Axis
				{
					// Window size on X (e.g. last 20 seconds if you tick at 10 Hz and step is 0.1)
					MinLimit = 0,
					MaxLimit = 20
				}
			];

			YAxes =
			[
				new Axis()
			];


			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(100) // 10 Hz
			};
			_timer.Tick += (_, __) => AddNextSample();
			_timer.Start();
		}

		private void AddNextSample()
		{
			const int maxPoints = 200;

			// Simple fake bandwidth-like signal:
			// base + sine + a bit of noise
			double noise = (Random.Shared.NextDouble() - 0.5) * 2.0; // [-1..+1]
			double value = 50.0 + 20.0 * Math.Sin(_t) + 5.0 * Math.Sin(_t * 0.2) + noise;

			Points.Add(new ObservablePoint(_x, value));

			// Keep only last N points
			if (Points.Count > maxPoints)
				Points.RemoveAt(0);

			// Scroll the visible window (X axis)
			XAxes[0].MinLimit = _x - 20;
			XAxes[0].MaxLimit = _x;

			_x += 0.1;   // x step (seconds)
			_t += 0.15;  // waveform param
		}
	}
}
