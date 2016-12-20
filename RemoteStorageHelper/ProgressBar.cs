using System;
using System.Text;
using System.Threading;

namespace RemoteStorageHelper
{
	/// <summary>
	/// An ASCII progress bar
	/// </summary>
	// Copyright (c) Daniel Wolf -- https://gist.github.com/DanielSWolf/0ab6a96899cc5377bf54
	public class ProgressBar : IDisposable, IProgress<double>
	{
		private const int BlockCount = 10;
		private readonly TimeSpan m_animationInterval = TimeSpan.FromSeconds(1.0 / 8);
		private const string Animation = @"|/-\";

		private readonly Timer m_timer;

		private double m_currentProgress;
		private string m_currentText = string.Empty;
		private bool m_disposed;
		private int m_animationIndex;

		public ProgressBar()
		{
			m_timer = new Timer(TimerHandler);

			// A progress bar is only for temporary display in a console window.
			// If the console output is redirected to a file, draw nothing.
			// Otherwise, we'll end up with a lot of garbage in the target file.
			if (!Console.IsOutputRedirected)
			{
				ResetTimer();
			}
		}

		public void Report(double value)
		{
			// Make sure value is in [0..1] range
			value = Math.Max(0, Math.Min(1, value));
			Interlocked.Exchange(ref m_currentProgress, value);
		}

		private void TimerHandler(object state)
		{
			lock (m_timer)
			{
				if (m_disposed) return;

				var progressBlockCount = (int)(m_currentProgress * BlockCount);
				var percent = (int)(m_currentProgress * 100);
				var text =
					$"[{new string('#', progressBlockCount)}{new string('-', BlockCount - progressBlockCount)}] {percent,3}% {Animation[m_animationIndex++ % Animation.Length]}";
				UpdateText(text);

				ResetTimer();
			}
		}

		private void UpdateText(string text)
		{
			// Get length of common portion
			var commonPrefixLength = 0;
			var commonLength = Math.Min(m_currentText.Length, text.Length);
			while (commonPrefixLength < commonLength && text[commonPrefixLength] == m_currentText[commonPrefixLength])
			{
				commonPrefixLength++;
			}

			// Backtrack to the first differing character
			var outputBuilder = new StringBuilder();
			outputBuilder.Append('\b', m_currentText.Length - commonPrefixLength);

			// Output new suffix
			outputBuilder.Append(text.Substring(commonPrefixLength));

			// If the new text is shorter than the old one: delete overlapping characters
			var overlapCount = m_currentText.Length - text.Length;
			if (overlapCount > 0)
			{
				outputBuilder.Append(' ', overlapCount);
				outputBuilder.Append('\b', overlapCount);
			}

			Console.Write(outputBuilder);
			m_currentText = text;
		}

		private void ResetTimer()
		{
			m_timer.Change(m_animationInterval, TimeSpan.FromMilliseconds(-1));
		}

		public void Dispose()
		{
			lock (m_timer)
			{
				m_disposed = true;
				UpdateText(string.Empty);
			}
		}
	}
}