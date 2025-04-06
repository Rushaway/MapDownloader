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
				
				// Utiliser un HashSet pour éliminer les doublons
				HashSet<string> bspFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				string[] lines = response.Split('\n');
				
				// Afficher les 10 premières lignes pour le débogage
				txtOutput.AppendText("Analyzing FastDL index page...");
				
				foreach (string line in lines)
				{
					// Rechercher les lignes contenant .bsp.bz2
					if (line.Contains(".bsp.bz2"))
					{
						// Extraire le nom du fichier
						string fileName = ExtractFileNameFromHtmlLine(line);
						if (!string.IsNullOrEmpty(fileName))
						{
							// Vérifier que c'est bien un fichier .bsp.bz2 et qu'il semble être un nom de carte valide
							if (fileName.EndsWith(".bsp.bz2") && IsValidMapName(fileName))
							{
								bspFiles.Add(fileName);
							}
						}
					}
				}
				
				// Convertir le HashSet en liste pour le traitement
				List<string> uniqueBspFiles = new List<string>(bspFiles);
				
				// Afficher les fichiers trouvés
				txtOutput.AppendText(Environment.NewLine + "Total unique .bsp.bz2 files found in fastDL: " + uniqueBspFiles.Count);
				
				// Vérifier quels fichiers doivent être téléchargés
				foreach (string file in uniqueBspFiles)
				{
					string mapName = file.Replace(".bsp.bz2", "");
					if (!downloadedMapList.Contains(mapName.ToLower()))
					{
						toDownloadList.Add(mapName);
					}
				}

				toDownloadCount = toDownloadList.Count;
				prgDownload.Maximum = toDownloadCount > 0 ? toDownloadCount : 1;
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

		private string ExtractFileNameFromHtmlLine(string line)
		{
			// Rechercher les fichiers .bsp.bz2
			int bspIndex = line.IndexOf(".bsp.bz2");
			if (bspIndex > 0)
			{
				// Vérifier si la ligne contient des balises HTML avec href
				if (line.Contains("href="))
				{
					// Format avec balise HTML
					int hrefIndex = line.IndexOf("href=");
					int startQuote = -1;
					
					// Chercher la citation après href=
					if (line.IndexOf('"', hrefIndex) > hrefIndex)
						startQuote = line.IndexOf('"', hrefIndex);
					else if (line.IndexOf('\'', hrefIndex) > hrefIndex)
						startQuote = line.IndexOf('\'', hrefIndex);
						
					if (startQuote >= 0)
					{
						char quoteChar = line[startQuote];
						int endQuote = line.IndexOf(quoteChar, startQuote + 1);
						
						if (endQuote >= 0)
						{
							string href = line.Substring(startQuote + 1, endQuote - startQuote - 1);
							
							// Si l'href contient le chemin complet, extraire juste le nom du fichier
							if (href.Contains("/"))
							{
								href = href.Substring(href.LastIndexOf('/') + 1);
							}
							
							// Vérifier que l'href contient .bsp.bz2
							if (href.Contains(".bsp.bz2"))
							{
								return href;
							}
						}
					}
				}
				else
				{
					// Format sans balise HTML (comme dans votre exemple)
					// Trouver le début du nom de fichier en cherchant le dernier espace avant .bsp.bz2
					int startIndex = 0;
					for (int i = bspIndex - 1; i >= 0; i--)
					{
						if (char.IsWhiteSpace(line[i]))
						{
							startIndex = i + 1;
							break;
						}
					}
					
					// Extraire le nom du fichier
					string fileName = line.Substring(startIndex, bspIndex + 8 - startIndex).Trim();
					
					// Vérifier que le nom ne contient pas de caractères HTML
					if (!fileName.Contains("<") && !fileName.Contains(">"))
					{
						return fileName;
					}
				}
			}
			
			return null;
		}

		// Fonction pour vérifier si un nom de fichier semble être une carte valide
		private bool IsValidMapName(string fileName)
		{
			// Vérifier que le nom se termine par .bsp.bz2
			if (!fileName.EndsWith(".bsp.bz2"))
				return false;
				
			// Enlever l'extension
			string mapName = fileName.Replace(".bsp.bz2", "");
			
			// Vérifier que le nom n'est pas vide
			if (string.IsNullOrWhiteSpace(mapName))
				return false;
				
			// Vérifier que le nom ne contient pas de caractères HTML
			if (mapName.Contains("<") || mapName.Contains(">") || mapName.Contains("&"))
				return false;
				
			// Vérifier que le nom ne contient pas de caractères spéciaux non valides pour un nom de fichier
			foreach (char c in Path.GetInvalidFileNameChars())
			{
				if (mapName.Contains(c))
					return false;
			}
			
			// Vérifier que le nom commence généralement par "ze_" (pour Zombie Escape) ou a un format de nom de carte
			// Cette vérification peut être adaptée selon vos besoins spécifiques
			if (!mapName.StartsWith("ze_") && !mapName.StartsWith("cs_") && !mapName.StartsWith("de_") && 
				!mapName.StartsWith("as_") && !mapName.StartsWith("aim_") && !mapName.StartsWith("surf_") && 
				!mapName.StartsWith("bhop_") && !mapName.StartsWith("kz_") && !mapName.StartsWith("mg_") && 
				!mapName.StartsWith("jb_") && !mapName.StartsWith("ba_") && !mapName.StartsWith("fy_"))
			{
				// Si le nom ne commence pas par un préfixe standard, vérifier qu'il a au moins une structure de nom de carte
				if (!mapName.Contains("_"))
					return false;
			}
			
			return true;
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
				
				// Nettoyer le nom de la carte de tout préfixe HTML
				if (currentMap.Contains("href="))
				{
					currentMap = currentMap.Substring(currentMap.IndexOf("href=") + 6);
				}
				
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
