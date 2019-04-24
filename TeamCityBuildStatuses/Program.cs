using System;
using System.IO;
using System.IO.Ports;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using Microsoft.Extensions.Configuration;

namespace TeamCityBuildStatuses
{ 
	public class Program
    {
	    private static Timer _timer;
	    private static string _lastBuildTime = "";
	    private static TCLogin _tcLogin;
		private static void Main(string[] args)
	    {
		    var builder = new ConfigurationBuilder()
			    .SetBasePath(Directory.GetCurrentDirectory())
			    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
			    .AddUserSecrets<Program>()
				.AddEnvironmentVariables();

			IConfigurationRoot configuration = builder.Build();
			_tcLogin = new TCLogin();
			configuration.GetSection("TCLogin").Bind(_tcLogin);

			SetTimer();
			//RunAsync().GetAwaiter().GetResult();
			//Console.ReadLine();
			//_timer.Stop();
			//_timer.Dispose();

			string input;
			do
			{
				input = ReadDataAndSendToArduino();
			} while (input != "9");
		}

		private static async Task<string> GetTeamCityInfoAsync()
		{
			var url = _tcLogin.URL;

			var client = new HttpClient();

			var byteArray = Encoding.ASCII.GetBytes(_tcLogin.Credentials);
			client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

			var response = await client.GetAsync(url);

			if (response.IsSuccessStatusCode)
			{
				var data = response.Content.ReadAsStringAsync().Result;
				XmlDocument xdoc = new XmlDocument();
				xdoc.LoadXml(data);
				var statusAttribute = xdoc.SelectSingleNode("//build/@status").Value;
				var finishDateAttribute = xdoc.SelectSingleNode("//finishDate").InnerText;

				Console.WriteLine(statusAttribute);
				Console.WriteLine(finishDateAttribute);

				if (_lastBuildTime != finishDateAttribute)
				{
					SendToArduino(statusAttribute);
					Console.WriteLine("posted to Arduino");
					_lastBuildTime = finishDateAttribute;
					return statusAttribute;
				}

				return "No change";
			}

			Console.WriteLine(response.StatusCode.ToString());
			return response.StatusCode.ToString();

		}

		public static void SetTimer()
		{
			// Create a timer with a twenty second interval.
			_timer = new Timer(20000);
			_timer.Elapsed += OnTimedEvent;
			_timer.AutoReset = true;
			_timer.Enabled = true;
		}

		private static void OnTimedEvent(object source, ElapsedEventArgs e)
		{
			GetTeamCityInfoAsync();
		}


		public static string ReadDataAndSendToArduino()
		{
			var input = Console.ReadLine();

			using (SerialPort port = new SerialPort("COM3", 4800))
			{
				if (!port.IsOpen)
				{
					port.Open();
				}
				port.Write(input);
				Console.WriteLine("colour = " + input);
			}

			return input;
		}

		private static void SendToArduino(string buildStatus)
		{
			using (SerialPort port = new SerialPort("COM3", 4800))
			{
				if (!port.IsOpen)
				{
					port.Open();
				}
				port.Write(buildStatus);
				Console.WriteLine("sent - " + buildStatus);
			}
		}

		static async Task RunAsync()
		{
			var buildStatus = await GetTeamCityInfoAsync();
			if (buildStatus == "SUCCESS" || buildStatus == "FAILURE")
			{
				Console.WriteLine("RunAsync - OK");
				SendToArduino(buildStatus);
			}
		}
	}
}