using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MapDownloader
{
	public partial class FrmStartup : Form
	{
		private List<Server> serverList = new List<Server>();
		private HttpClient client = new HttpClient();

		public FrmStartup()
		{
			InitializeComponent();
		}

		private async void frmStartup_Load(object sender, EventArgs e)
		{
			try
			{
				await LoadServersAsync();
			}
			catch (Exception ex)
			{
				MessageBox.Show("Error loading servers: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private async Task LoadServersAsync()
		{
			try
			{
				string serversUrl = "https://raw.githubusercontent.com/NiDE-gg/MapDownloader/master/servers.json";
				
				string response = await client.GetStringAsync(serversUrl);
				string[] lines = response.Split('\n');
				
				serverList.Clear();
				lbServers.Items.Clear();
				
				foreach (string line in lines)
				{
					string trimmedLine = line.Trim();
					if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("#"))
					{
						string[] parts = trimmedLine.Split('|');
						if (parts.Length >= 3)
						{
							Server server = new Server
							{
								Name = parts[0].Trim(),
								FastDlUrl = parts[1].Trim(),
								MaplistUrl = parts.Length > 2 ? parts[2].Trim() : ""
							};
							
							serverList.Add(server);
							lbServers.Items.Add(server.Name);
						}
					}
				}
				
				if (lbServers.Items.Count > 0)
				{
					lbServers.SelectedIndex = 0;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Error loading servers: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void lbServers_SelectedIndexChanged(object sender, EventArgs e)
		{
			// Vérifier que l'index sélectionné est valide
			if (lbServers.SelectedIndex >= 0 && lbServers.SelectedIndex < serverList.Count)
			{
				Server selectedServer = serverList[lbServers.SelectedIndex];
				txtFastdlUrl.Text = selectedServer.FastDlUrl;
				txtMaplistUrl.Text = selectedServer.MaplistUrl;
			}
		}

		private void btnStart_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(txtFastdlUrl.Text))
			{
				MessageBox.Show("Please select a server or enter a FastDL URL", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			
			Global.fastdlUrl = txtFastdlUrl.Text;
			Global.maplistUrl = txtMaplistUrl.Text;
			
			FrmMain frmMain = new FrmMain();
			this.Hide();
			frmMain.ShowDialog();
			this.Close();
		}
	}

	public class Server
	{
		public string Name { get; set; }
		public string FastDlUrl { get; set; }
		public string MaplistUrl { get; set; }
	}
}
