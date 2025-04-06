using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
				
				using (JsonDocument doc = JsonDocument.Parse(response))
				{
					JsonElement root = doc.RootElement;
					JsonElement serversArray = root.GetProperty("servers");
					
					serverList.Clear();
					lbServers.Items.Clear();
					
					foreach (JsonElement serverElement in serversArray.EnumerateArray())
					{
						Server server = new Server
						{
							Name = serverElement.GetProperty("name").GetString(),
							FastDlUrl = serverElement.GetProperty("fastDL").GetString(),
							AppID = serverElement.GetProperty("appID").GetString(),
							MapsDirectory = serverElement.GetProperty("mapsDirectory").GetString()
						};
						
						serverList.Add(server);
						lbServers.Items.Add(server.Name);
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
			if (lbServers.SelectedIndex >= 0 && lbServers.SelectedIndex < serverList.Count)
			{
				Server selectedServer = serverList[lbServers.SelectedIndex];
				txtFastdlUrl.Text = selectedServer.FastDlUrl;
				
				Global.appID = selectedServer.AppID;
				Global.mapsDirectory = selectedServer.MapsDirectory;
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
		public string AppID { get; set; }
		public string MapsDirectory { get; set; }
	}
}
