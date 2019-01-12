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
                Task t = Task.Run(() =>
                 {
                     try
                     {
                         List<string> command = new List<string>();
                         string gitUrl = _config["giturl"];
                         var gitName = gitUrl.Substring(gitUrl.IndexOf('/') + 1);
                         gitName = gitName.Substring(0, gitName.LastIndexOf(".git"));
                         var path = _config["clonePath"];
                         var destPath = Path.Combine(_config["clonePath"], gitName);
                         if (!Directory.Exists(path))
                         {
                             Directory.CreateDirectory(path);
                         }
                         if (Directory.Exists(destPath))
                         {
                             command.Add("cd " + destPath);
                             _logger.LogDebug("add command:" + command[command.Count - 1]);
                             command.Add("git pull origin master");
                             _logger.LogDebug("add command:" + command[command.Count - 1]);
                         }
                         else
                         {
                             command.Add("cd " + path);
                             _logger.LogDebug("add command:" + command[command.Count - 1]);
                             command.Add("git clone " + _config["giturl"]);
                             _logger.LogDebug("add command:" + command[command.Count - 1]);
                         }
                         using (Process proc = new Process())
                         {
                             proc.StartInfo.FileName = "bash";
                             proc.StartInfo.UseShellExecute = false;
                             proc.StartInfo.RedirectStandardOutput = true;
                             proc.StartInfo.RedirectStandardError = true;
                             proc.StartInfo.RedirectStandardInput = true;
                             proc.OutputDataReceived += new DataReceivedEventHandler(process_OutputDataReceived);
                             proc.ErrorDataReceived += new DataReceivedEventHandler(Process_ErrorDataReceived);
                             proc.Start();
                             proc.BeginOutputReadLine();
                             proc.BeginErrorReadLine();
                             foreach (var c in command)
                             {
                                 proc.StandardInput.WriteLine(c);
                             }
                             proc.StandardInput.WriteLine("exit");
                             proc.WaitForExit();
                         }
                         _logger.LogDebug("git over!");
                     }
                     catch (Exception ex)
                     {
                         _logger.LogError(ex, "git exction");
                     }
                 });
                if (t.IsCompletedSuccessfully)
                {
                    _logger.LogDebug("command run success");
                }
                else
                {
                    _logger.LogDebug("command over but not success");
                }
            }
            else
            {
                _logger.LogDebug("check failed");
            }
            return Ok();

        }


        private void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {

            _logger.LogDebug("执行数据：" + e.Data);

        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            _logger.LogError("错误数据：" + e.Data);
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
