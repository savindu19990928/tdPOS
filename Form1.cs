using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace PposSystem
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string username = textBox1.Text.Trim();
            string password = textBox2.Text.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter username and password");
                return;
            }

            string url = "https://ppos-318745238310.us-east1.run.app/api/auth/login/";

            var loginData = new
            {
                username,
                password
            };
            string jsonData = JsonConvert.SerializeObject(loginData);

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(jsonData);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (var streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        string responseText = streamReader.ReadToEnd();

                        // Deserialize safely as dictionary
                        var result = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseText);

                        if (result != null && result.ContainsKey("access"))
                        {
                            string accessToken = result["access"];

                            // Pass token to next form
                            sale_system form = new sale_system(accessToken,username);
                            form.Show();
                            this.Hide();
                        }
                        else
                        {
                            MessageBox.Show("Login failed: access token not found.\nResponse:\n" + responseText);
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                string errorResponse = "";
                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                    {
                        errorResponse = reader.ReadToEnd();
                    }
                }
                MessageBox.Show("Please Enter Correct Credentials!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Please Enter Correct Credentials!");
            }
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                textBox2.Focus();
            }
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button1.PerformClick();
            }
        }
    }
}
