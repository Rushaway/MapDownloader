using ICSharpCode.SharpZipLib.BZip2;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MapDownloader
{
	public partial class FrmMain : Form
	{
		private HttpClient client = new HttpClient();
		private Queue<string> queue = new Queue<string>();
		private bool running = false;
		private int processed = 0;
		private int toDownloadCount;
		private string currentMap;
		private bool currentCompressed;

		public FrmMain()
		{
			InitializeComponent();
		}

		private void FrmMain_Load(object sender, EventArgs e)
		{
			txtMapsDir.Text = Functions.GetMapsDirectory();
			txtMapsDir.SelectionStart = 0;
		}

		private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (running)
			{
				if (MessageBox.Show("A download process is currently running, are you sure you want to exit?" + Environment.NewLine + Environment.NewLine + "Exiting while a process is running could result in map file corruption", "Exit Confirmation", MessageBoxButtons.YesNo) == DialogResult.No)
				{
					e.Cancel = true;
				}
			}
		}

		private void tlsAbout_Click(object sender, EventArgs e)
		{
			FrmAbout frmAbout = new FrmAbout();
			frmAbout.ShowDialog();
		}

		private void btnBrowse_Click(object sender, EventArgs e)
		{
			FolderBrowserDialog dialog = new FolderBrowserDialog();

			if (dialog.ShowDialog() == DialogResult.OK)
				txtMapsDir.Text = dialog.SelectedPath + "\\";
		}

		private async void btnMain_Click_Download(object sender, EventArgs e)
		{
			ToggleMode(false);
			processed = 0;

			List<string> downloadedMapList = new List<string>();
			List<string> toDownloadList = new List<string>();
			FileInfo[] mapFiles;

			txtOutput.Text = "";

			try
			{
				// Note: HttpClient handles certificate validation differently
				// If you need to bypass certificate validation, use:
				// var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true };
				// client = new HttpClient(handler);
			}
			catch (Exception ex)
			{
				txtOutput.AppendText("ERROR: " + ex.Message);
				ToggleMode(true);
				return;
			}

			try
			{
				mapFiles = new DirectoryInfo(txtMapsDir.Text).GetFiles("*.bsp");
			}
			catch (Exception)
			{
				txtOutput.AppendText("ERROR: Invalid maps directory provided");
				ToggleMode(true);
				return;
			}

			foreach (FileInfo file in mapFiles)
				downloadedMapList.Add(file.Name.Split('.')[0].ToLower());

			// Logic for dl all fastdl maps
			try
			{
				string response = await client.GetStringAsync(Global.fastdlUrl);
				string[] fastdlFiles = response.Split('\n');

				foreach (string rawFile in fastdlFiles)
				{
					string file = rawFile.Replace("\r\n", "").Replace("\n", "");

					if (!file.Equals(""))
					{
						if (!downloadedMapList.Contains(file.Replace("$", "").ToLower()))
							toDownloadList.Add(file);
					}
				}

				txtOutput.AppendText("Total files found in fastDL: " + fastdlFiles.Length);

				toDownloadCount = toDownloadList.Count;
				prgDownload.Maximum = toDownloadCount;
				prgDownload.Value = 0;
				prgDownload.Step = 1;

				if (toDownloadCount != 0)
				{
					if (toDownloadCount == 1)
						txtOutput.AppendText(Environment.NewLine + "Maps directory missing " + toDownloadCount + " map from the fastDL, marking it for download...");
					else
						txtOutput.AppendText(Environment.NewLine + "Maps directory missing " + toDownloadCount + " maps from the fastDL, marking them for download...");

					foreach (string file in toDownloadList)
						queue.Enqueue(file);

					await DownloadAsync();
				}
				else
				{
					txtOutput.AppendText(Environment.NewLine + "All maps already downloaded and up to date!");
					ToggleMode(true);
				}
			}
			catch (Exception ex)
			{
				txtOutput.AppendText(Environment.NewLine + "ERROR: " + ex.Message);
				ToggleMode(true);
			}
		}

		private void btnMain_Click_Stop(object sender, EventArgs e)
		{
			txtOutput.AppendText(Environment.NewLine + "Stop request received, process will stop after the current map is finished");
			btnMain.Enabled = false;
			queue.Clear();
		}

		private async Task DownloadAsync()
		{
			if (queue.Count > 0)
			{
				currentMap = queue.Dequeue();

				if (currentMap.StartsWith("$"))
				{
					currentMap = currentMap.Replace("$", "");
					currentCompressed = false;
				}
				else
				{
					currentCompressed = true;
				}

				txtOutput.AppendText(Environment.NewLine + "Downloading " + currentMap);

				try
				{
					string fileUrl = Global.fastdlUrl + currentMap + (currentCompressed ? ".bsp.bz2" : ".bsp");
					string filePath = txtMapsDir.Text + currentMap + (currentCompressed ? ".bsp.bz2" : ".bsp");

					using (var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
					{
						response.EnsureSuccessStatusCode();
						using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
						using (var stream = await response.Content.ReadAsStreamAsync())
						{
							await stream.CopyToAsync(fileStream);
						}
					}

					await ProcessDownloadedFileAsync();
				}
				catch (Exception ex)
				{
					txtOutput.AppendText(Environment.NewLine + currentMap + " download failed: " + ex.Message);
					prgDownload.PerformStep();
					await DownloadAsync();
				}
			}
			else
			{
				if (processed == 1)
					txtOutput.AppendText(Environment.NewLine + "Successfully downloaded/extracted " + processed + " map");
				else
					txtOutput.AppendText(Environment.NewLine + "Successfully downloaded/extracted " + processed + " maps");

				ToggleMode(true);
				btnMain.Enabled = true;
				prgDownload.Maximum = processed;

				FlashWindow.Flash(this);
			}
		}

		private async Task ProcessDownloadedFileAsync()
		{
			FileInfo compressedFile = new FileInfo(txtMapsDir.Text + currentMap + ".bsp.bz2");

			try
			{
				if (currentCompressed)
				{
					using (FileStream compressedStream = compressedFile.OpenRead())
					using (FileStream decompressedStream = File.Create(txtMapsDir.Text + currentMap + ".bsp"))
					{
						txtOutput.AppendText(Environment.NewLine + "Extracting " + currentMap);
						await Task.Run(() => BZip2.Decompress(compressedStream, decompressedStream, true));
					}
				}

				processed++;
				prgDownload.PerformStep();

				if (compressedFile.Exists)
					compressedFile.Delete();

				await DownloadAsync();
			}
			catch (Exception ex)
			{
				txtOutput.AppendText(Environment.NewLine + currentMap + " extraction failed: " + ex.Message);
				prgDownload.PerformStep();
				await DownloadAsync();
			}
		}

		private void ToggleMode(bool defaultState)
		{
			if (defaultState)
			{
				btnMain.Text = "Download Maps";
				this.btnMain.Click -= new EventHandler(this.btnMain_Click_Stop);
				this.btnMain.Click += new EventHandler(this.btnMain_Click_Download);
			}
			else
			{
				btnMain.Text = "Stop";
				this.btnMain.Click -= new EventHandler(this.btnMain_Click_Download);
				this.btnMain.Click += new EventHandler(this.btnMain_Click_Stop);
			}

			running = !defaultState;
			btnBrowse.Enabled = defaultState;
			txtMapsDir.Enabled = defaultState;
		}
	}
}
