using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace CoreHooks.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GithubController : ControllerBase
    {
        private readonly ILogger<GithubController> _logger;
        private readonly IConfiguration _config;
        public GithubController(IConfiguration configuration, ILogger<GithubController> logger)
        {
            _config = configuration;
            _logger = logger;
        }

        [HttpPost]
        [Route("[action]")]
        public IActionResult GitHook()
        {
            bool isSuccess = true;
            if (Request.Headers.ContainsKey("X-GitHub-Event"))
            {
                _logger.LogDebug("found X-GitHub-Event,check!");
                var eventName = Request.Headers["X-GitHub-Event"];
                isSuccess = isSuccess && eventName == "push";
            }
            else
            {
                _logger.LogDebug("not found X-GitHub-Event");
            }
            if (Request.Headers.ContainsKey("X-GitHub-Delivery"))
            {
                _logger.LogDebug("found X-GitHub-Delivery,check!");
                var delivery = Request.Headers["X-GitHub-Delivery"];
                isSuccess = isSuccess && !string.IsNullOrEmpty(delivery);
            }
            else
            {
                _logger.LogDebug("not found X-GitHub-Delivery");
            }
            if (Request.Headers.ContainsKey("X-Hub-Signature"))
            {
                _logger.LogDebug("found X-Hub-Signature,check!");
                var mac = Request.Headers["X-Hub-Signature"];
                string signature = Request.Headers["X-Hub-Signature"];
                StreamReader reader = new StreamReader(Request.Body);
                string json = reader.ReadToEnd();
                var hmac = SignWithHmac(UTF8Encoding.UTF8.GetBytes(json), UTF8Encoding.UTF8.GetBytes(_config["SecretKey"]));
                var hmacHex = ConvertToHexadecimal(hmac);
                bool isValid = signature.Split('=')[1] == hmacHex;
                isSuccess = isSuccess && isValid;
            }
            else
            {
                _logger.LogDebug("not found X-Hub-Signature!");
            }
            if (Request.Headers.ContainsKey("User-Agent"))
            {
                _logger.LogDebug("found User-Agent,check!");
                var agent = Request.Headers["User-Agent"].ToString().ToLower();
                isSuccess = isSuccess && agent.Contains("github");
            }
            else
            {
                _logger.LogDebug("not found User-Agent!");
            }
            if (isSuccess)
            {
                _logger.LogDebug("check success git begin!");
                Task.Run(() =>
                {

                    try
                    {
                        using (Process p = new Process())
                        {
                            string gitUrl = _config["giturl"];
                            var gitName = gitUrl.Substring(gitUrl.IndexOf('/') + 1).Replace(".git", "");
                            p.StartInfo = new ProcessStartInfo();
                            p.StartInfo.FileName = "bash";
                            p.StartInfo.UseShellExecute = false;
                            p.StartInfo.RedirectStandardInput = true;
                            p.StartInfo.RedirectStandardOutput = true;
                            p.StartInfo.RedirectStandardError = true;
                            p.StartInfo.CreateNoWindow = true;
                            p.OutputDataReceived += new DataReceivedEventHandler(process_OutputDataReceived);
                            p.ErrorDataReceived += new DataReceivedEventHandler(Process_ErrorDataReceived);
                            p.Start();
                            var path = _config["clonePath"];
                            var destPath = Path.Combine(_config["clonePath"], gitName);
                            if (!Directory.Exists(path))
                            {
                                Directory.CreateDirectory(path);
                            }
                            if (Directory.Exists(destPath))
                            {
                                p.StandardInput.WriteLine("cd " + path);
                                p.StandardInput.WriteLine("git pull origin master");
                            }
                            else
                            {
                                p.StandardInput.WriteLine("cd " + path);
                                p.StandardInput.WriteLine("git clone " + _config["giturl"]);
                                
                            }
                            
                            _logger.LogDebug("git over!");
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "git exction");
                    }
                });
            }
            else
            {
                _logger.LogDebug("check failed");
            }
            return Ok();

        }

        private void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Data))
            {
                _logger.LogDebug("执行数据：" + e.Data);
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Data))
            {
                _logger.LogError("执行数据：" + e.Data);
            }
        }

        private byte[] SignWithHmac(byte[] dataToSign, byte[] keyBody)
        {
            using (var hmacAlgorithm = new System.Security.Cryptography.HMACSHA1(keyBody))
            {
                return hmacAlgorithm.ComputeHash(dataToSign);
            }
        }

        private string ConvertToHexadecimal(IEnumerable<byte> bytes)
        {
            var builder = new StringBuilder();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }

    }
}