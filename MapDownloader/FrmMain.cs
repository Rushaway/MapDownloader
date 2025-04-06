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

			try
			{
				string response = await client.GetStringAsync(Global.fastdlUrl);
				
				// Extraire les noms de fichiers .bsp.bz2 de la page d'index
				List<string> bspFiles = new List<string>();
				string[] lines = response.Split('\n');
				
				foreach (string line in lines)
				{
					// Rechercher les lignes contenant .bsp.bz2
					if (line.Contains(".bsp.bz2"))
					{
						// Extraire le nom du fichier
						string fileName = ExtractFileNameFromHtmlLine(line);
						if (!string.IsNullOrEmpty(fileName))
						{
							bspFiles.Add(fileName);
						}
					}
				}
				
				txtOutput.AppendText("Total .bsp.bz2 files found in fastDL: " + bspFiles.Count);
				
				// Vérifier quels fichiers doivent être téléchargés
				foreach (string file in bspFiles)
				{
					string mapName = file.Replace(".bsp.bz2", "");
					if (!downloadedMapList.Contains(mapName.ToLower()))
					{
						toDownloadList.Add(mapName);
					}
				}

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

		// Fonction pour extraire le nom de fichier d'une ligne HTML
		private string ExtractFileNameFromHtmlLine(string line)
		{
			// Rechercher les fichiers .bsp.bz2
			int bspIndex = line.IndexOf(".bsp.bz2");
			if (bspIndex > 0)
			{
				// Trouver le début du nom de fichier
				int startIndex = line.LastIndexOf('>', bspIndex);
				if (startIndex >= 0)
				{
					startIndex++; // Avancer après le '>'
				}
				else
				{
					// Si pas de balise HTML, chercher un espace ou une tabulation
					startIndex = 0;
					for (int i = 0; i < bspIndex; i++)
					{
						if (char.IsWhiteSpace(line[i]))
						{
							startIndex = i + 1;
						}
					}
				}
				
				// Extraire le nom du fichier
				string fileName = line.Substring(startIndex, bspIndex + 8 - startIndex).Trim();
				return fileName;
			}
			
			return null;
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
				currentCompressed = true; // Toujours compressé pour les .bsp.bz2
				
				txtOutput.AppendText(Environment.NewLine + "Downloading " + currentMap);
				
				try
				{
					// Assurez-vous que l'URL est correctement formatée
					string fileUrl = Global.fastdlUrl;
					if (!fileUrl.EndsWith("/"))
						fileUrl += "/";
						
					fileUrl += currentMap + ".bsp.bz2";
					
					string filePath = txtMapsDir.Text + currentMap + ".bsp.bz2";
					
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
