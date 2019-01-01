using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebHooks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.IO;

namespace CoreHooks.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GitHubController : ControllerBase
    {
        private readonly IConfiguration _config;
        public GitHubController(IConfiguration configuration)
        {
            _config = configuration;

        }
        [GitHubWebHook]
        public IActionResult GitHubHandler(string id, string @event, JObject data)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (@event == "push")
            {
                Task.Run(() =>
                {
                    string gitUrl = _config["giturl"];
                    var gitName = gitUrl.Substring(gitUrl.IndexOf('/')+1).Replace(".git","");
                    Process p = new Process();
                    p.StartInfo = new ProcessStartInfo();
                    p.StartInfo.FileName = "bash";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardInput = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.StartInfo.CreateNoWindow = true;
                    p.Start();
                    var path = _config["clonePath"];
                    if (Directory.Exists(path))
                    {
                        p.StandardInput.WriteLine("cd " +Path.Combine(path,gitName));
                        p.StandardInput.WriteLine("git pull origin master");
                    }
                    else
                    {
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                        }
                        p.StandardInput.WriteLine("cd " + path);
                        p.StandardInput.WriteLine("git clone " + _config["giturl"]);
                    }

                    p.Close();
                });
            }
            return Ok();
        }

    }
}
