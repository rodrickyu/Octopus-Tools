using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using log4net;
using Octopus.Cli.Diagnostics;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octopus.Cli.Commands
{
    public abstract class ApiCommand : ICommand
    {
        readonly ILog log;
        readonly IOctopusRepositoryFactory repositoryFactory;
        readonly IOctopusFileSystem fileSystem;
        string apiKey;
        bool enableDebugging;
        bool ignoreSslErrors;
        string password;
        IOctopusRepository repository;
        string serverBaseUrl;
        string username;
        readonly Options optionGroups = new Options();

        protected ApiCommand(IOctopusRepositoryFactory repositoryFactory, ILog log, IOctopusFileSystem fileSystem)
        {
            this.repositoryFactory = repositoryFactory;
            this.log = log;
            this.fileSystem = fileSystem;

            var options = optionGroups.For("Common options");
            options.Add("server=", "The base URL for your Octopus server - e.g., http://your-octopus/", v => serverBaseUrl = v);
            options.Add("apiKey=", "Your API key. Get this from the user profile page.", v => apiKey = v);
            options.Add("user=", "[Optional] Username to use when authenticating with the server.", v => username = v);
            options.Add("pass=", "[Optional] Password to use when authenticating with the server.", v => password = v);
            options.Add("configFile=", "[Optional] Text file of default values, with one 'key = value' per line.", v => ReadAdditionalInputsFromConfigurationFile(v));
            options.Add("debug", "[Optional] Enable debug logging", v => enableDebugging = true);
            options.Add("ignoreSslErrors", "[Optional] Set this flag if your Octopus server uses HTTPS but the certificate is not trusted on this machine. Any certificate errors will be ignored. WARNING: this option may create a security vulnerability.", v => ignoreSslErrors = true);
            options.Add("enableServiceMessages", "[Optional] Enable TeamCity or Team Foundation Build service messages when logging.", v => log.EnableServiceMessages());
        }

        protected Options Options { get { return optionGroups; } }

        protected ILog Log
        {
            get { return log; }
        }

        protected string ServerBaseUrl
        {
            get { return serverBaseUrl; }
        }

        protected IOctopusRepository Repository
        {
            get { return repository; }
        }

        protected IOctopusFileSystem FileSystem
        {
            get {  return fileSystem; }
        }

        public void GetHelp(TextWriter writer)
        {
            optionGroups.WriteOptionDescriptions(writer);
        }

        public void Execute(string[] commandLineArguments)
        {
            var remainingArguments = optionGroups.Parse(commandLineArguments);
            if (remainingArguments.Count > 0)
                throw new CommandException("Unrecognized command arguments: " + string.Join(", ", remainingArguments));

            if (string.IsNullOrWhiteSpace(serverBaseUrl))
                throw new CommandException("Please specify the Octopus Server URL using --server=http://your-server/");

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new CommandException("Please specify your API key using --apiKey=ABCDEF123456789. Learn more at: https://github.com/OctopusDeploy/Octopus-Tools");

            var credentials = ParseCredentials(username, password);

            var endpoint = new OctopusServerEndpoint(serverBaseUrl, apiKey, credentials);

            repository = repositoryFactory.CreateRepository(endpoint);

            if (enableDebugging)
            {
                repository.Client.SendingOctopusRequest += request => log.Debug(request.Method + " " + request.Uri);
            }

            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
            {
                if (errors == SslPolicyErrors.None)
                {
                    return true;
                }

                var certificate2 = (X509Certificate2) certificate;
                var warning = "The following certificate errors were encountered when establishing the HTTPS connection to the server: " + errors + Environment.NewLine +
                                 "Certificate subject name: " + certificate2.SubjectName.Name + Environment.NewLine +
                                 "Certificate thumbprint:   " + ((X509Certificate2) certificate).Thumbprint;

                if (ignoreSslErrors)
                {
                    log.Warn(warning);
                    log.Warn("Because --ignoreSslErrors was set, this will be ignored.");
                    return true;
                }

                log.Error(warning);
                return false;
            };

            log.Debug("Handshaking with Octopus server: " + serverBaseUrl);
            var root = repository.Client.RootDocument;
            log.Debug("Handshake successful. Octopus version: " + root.Version + "; API version: " + root.ApiVersion);

            var user = repository.Users.GetCurrent();
            if (user != null)
            {
                log.DebugFormat("Authenticated as: {0} <{1}> {2}", user.DisplayName, user.EmailAddress, user.IsService ? "(a service account)" : "");
            }

            Execute();
        }

        protected abstract void Execute();

        static NetworkCredential ParseCredentials(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                return CredentialCache.DefaultNetworkCredentials;

            var split = username.Split('\\');
            if (split.Length == 2)
            {
                var domain = split.First();
                username = split.Last();

                return new NetworkCredential(username, password, domain);
            }

            return new NetworkCredential(username, password);
        }

        protected List<string> ReadAdditionalInputsFromConfigurationFile(string configFile)
        {
            configFile = fileSystem.GetFullPath(configFile);

            log.Debug("Loading additional arguments from config file: " + configFile);

            if (!fileSystem.FileExists(configFile))
            {
                throw new CommandException("Unable to find config file " + configFile);
            }

            var results = new List<string>();
            using (var fileStream = fileSystem.OpenFile(configFile, FileAccess.Read))
            using (var file = new StreamReader(fileStream))
            {
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#"))
                    {
                        results.Add("--" + line.Trim());
                    }
                }
            }

            var remainingArguments = optionGroups.Parse(results);
            if (remainingArguments.Count > 0)
                throw new CommandException("Unrecognized arguments in configuration file: " + string.Join(", ", remainingArguments));

            return results;
        }

        protected string GetPortalUrl(string path)
        {
            if (!path.StartsWith("/")) path = '/' + path;
            var uri = new Uri(serverBaseUrl + path);
            return uri.AbsoluteUri;
        }

        protected static IEnumerable<string> FormatReleasePropertiesAsStrings(ReleaseResource release)
        {
            return new List<string>
            {
                "Version: " + release.Version,
                "Assembled: " + release.Assembled,
                "Package Versions: " + GetPackageVersionsAsString(release.SelectedPackages),
                "Release Notes: " + ((release.ReleaseNotes != null) ? release.ReleaseNotes.Replace(Environment.NewLine, @"\n") : "")
            };
        }

        protected static string GetPackageVersionsAsString(IEnumerable<SelectedPackage> packages)
        {
            var packageVersionsAsString = "";

            foreach (var package in packages)
            {
                var packageVersionAsString = package.StepName + " " + package.Version;

                if (packageVersionsAsString.Contains(packageVersionAsString))
                {
                    continue;
                }
                if (!String.IsNullOrEmpty(packageVersionsAsString))
                {
                    packageVersionsAsString += "; ";
                }
                packageVersionsAsString += packageVersionAsString;
            }
            return packageVersionsAsString;
        }
    }
}