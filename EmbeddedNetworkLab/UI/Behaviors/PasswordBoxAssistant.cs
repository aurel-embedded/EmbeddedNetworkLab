using System;
using System.Windows;
using System.Windows.Controls;

namespace EmbeddedNetworkLab.UI.Behaviors
{
	/// <summary>
	/// Attached properties to allow binding the PasswordBox.Password property.
	/// </summary>
	public static class PasswordBoxAssistant
	{
		public static readonly DependencyProperty BoundPasswordProperty =
			DependencyProperty.RegisterAttached(
				"BoundPassword",
				typeof(string),
				typeof(PasswordBoxAssistant),
				new FrameworkPropertyMetadata(string.Empty, OnBoundPasswordChanged));

		public static readonly DependencyProperty BindPasswordProperty =
			DependencyProperty.RegisterAttached(
				"BindPassword",
				typeof(bool),
				typeof(PasswordBoxAssistant),
				new PropertyMetadata(false, OnBindPasswordChanged));

		private static readonly DependencyProperty UpdatingPasswordProperty =
			DependencyProperty.RegisterAttached(
				"UpdatingPassword",
				typeof(bool),
				typeof(PasswordBoxAssistant));

		public static string GetBoundPassword(DependencyObject dp) => (string)dp.GetValue(BoundPasswordProperty);
		public static void SetBoundPassword(DependencyObject dp, string value) => dp.SetValue(BoundPasswordProperty, value);

		public static bool GetBindPassword(DependencyObject dp) => (bool)dp.GetValue(BindPasswordProperty);
		public static void SetBindPassword(DependencyObject dp, bool value) => dp.SetValue(BindPasswordProperty, value);

		private static bool GetUpdatingPassword(DependencyObject dp) => (bool)dp.GetValue(UpdatingPasswordProperty);
		private static void SetUpdatingPassword(DependencyObject dp, bool value) => dp.SetValue(UpdatingPasswordProperty, value);

		private static void OnBoundPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
		{
			if (dp is PasswordBox box)
			{
				box.PasswordChanged -= HandlePasswordChanged;

				if (!GetUpdatingPassword(box))
				{
					box.Password = (string?)e.NewValue ?? string.Empty;
				}

				box.PasswordChanged += HandlePasswordChanged;
			}
		}

		private static void OnBindPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
		{
			if (dp is PasswordBox box)
			{
				bool wasBound = (bool)e.OldValue;
				bool needBound = (bool)e.NewValue;

				if (wasBound)
					box.PasswordChanged -= HandlePasswordChanged;

				if (needBound)
					box.PasswordChanged += HandlePasswordChanged;
			}
		}

		private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
		{
			if (sender is not PasswordBox box)
				return;

			SetUpdatingPassword(box, true);
			SetBoundPassword(box, box.Password);
			SetUpdatingPassword(box, false);
		}
	}
}
