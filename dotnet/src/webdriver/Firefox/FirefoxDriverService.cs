﻿// <copyright file="FirefoxDriverService.cs" company="WebDriver Committers">
// Licensed to the Software Freedom Conservancy (SFC) under one
// or more contributor license agreements. See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership. The SFC licenses this file
// to you under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenQA.Selenium.Internal;
using System.Net;
using OpenQA.Selenium.Remote;

namespace OpenQA.Selenium.Firefox
{
    /// <summary>
    /// Exposes the service provided by the native FirefoxDriver executable.
    /// </summary>
    public sealed class FirefoxDriverService : DriverService
    {
        private const string FirefoxDriverServiceFileName = "wires.exe";
        private static readonly Uri FirefoxDriverDownloadUrl = new Uri("https://github.com/jgraham/wires/releases");
        private string browserBinaryPath = @"C:\Program Files (x86)\Nightly\firefox.exe";
        private int browserCommunicationPort = -1;

        /// <summary>
        /// Initializes a new instance of the FirefoxDriverService class.
        /// </summary>
        /// <param name="executablePath">The full path to the Firefox driver executable.</param>
        /// <param name="executableFileName">The file name of the Firefox driver executable.</param>
        /// <param name="port">The port on which the Firefox driver executable should listen.</param>
        private FirefoxDriverService(string executablePath, string executableFileName, int port)
            : base(executablePath, port, executableFileName, FirefoxDriverDownloadUrl)
        {
        }

        /// <summary>
        /// Gets or sets the location of the Firefox binary executable.
        /// </summary>
        public string FirefoxBinaryPath
        {
            get { return this.browserBinaryPath; }
            set { this.browserBinaryPath = value; }
        }

        /// <summary>
        /// Gets or sets the port used by the driver executable to communicate with the browser.
        /// </summary>
        public int BrowserCommunicationPort
        {
            get { return this.browserCommunicationPort; }
            set { this.browserCommunicationPort = value; }
        }

        /// <summary>
        /// Gets a value indicating the time to wait for an initial connection before timing out.
        /// </summary>
        protected override TimeSpan InitializationTimeout
        {
            get { return TimeSpan.FromSeconds(2); }
        }

        /// <summary>
        /// Gets a value indicating the time to wait for the service to terminate before forcing it to terminate.
        /// </summary>
        protected override TimeSpan TerminationTimeout
        {
            // Use a very small timeout for terminating the Firefox driver,
            // because the executable does not have a clean shutdown command,
            // which means we have to kill the process. Using a short timeout
            // gets us to the termination point much faster.
            get { return TimeSpan.FromMilliseconds(100); }
        }

        /// <summary>
        /// Gets a value indicating whether the service is responding to HTTP requests.
        /// </summary>
        protected override bool IsInitialized
        {
            get
            {
                bool isInitialized = false;
                try
                {
                    // Since Firefox driver won't implement the /session end point (because
                    // the W3C spec working group stupidly decided that it isn't necessary),
                    // we'll attempt to poll for a different URL which has no side effects.
                    // We've chosen to poll on the "quit" URL, passing in a nonexistent
                    // session id.
                    Uri serviceHealthUri = new Uri(this.ServiceUrl, new Uri("/session/FakeSessionIdForPollingPurposes", UriKind.Relative));
                    HttpWebRequest request = HttpWebRequest.Create(serviceHealthUri) as HttpWebRequest;
                    request.KeepAlive = false;
                    request.Timeout = 5000;
                    request.Method = "DELETE";
                    HttpWebResponse response = request.GetResponse() as HttpWebResponse;

                    // Checking the response from deleting a nonexistent session. Note that we are simply
                    // checking that the HTTP status returned is a 200 status, and that the resposne has
                    // the correct Content-Type header. A more sophisticated check would parse the JSON
                    // response and validate its values. At the moment we do not do this more sophisticated
                    // check.
                    isInitialized = response.StatusCode == HttpStatusCode.OK && response.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
                    response.Close();
                }
                catch (WebException ex)
                {
                    // Because the Firefox driver (incorrectly) does not allow quit on a
                    // nonexistent session to succeed, this will throw a WebException,
                    // which means we're reduced to using exception handling for flow control.
                    // This situation is highly undesirable, and in fact is a horrible code
                    // smell, but the implementation leaves us no choice. So we will check for
                    // the known response code and content type header, just like we would for
                    // the success case. Either way, a valid HTTP response instead of a socket
                    // error would tell us that the HTTP server is responding to requests, which
                    // is really what we want anyway.
                    HttpWebResponse errorResponse = ex.Response as HttpWebResponse;
                    if (errorResponse != null)
                    {
                        isInitialized = errorResponse.StatusCode == HttpStatusCode.InternalServerError && errorResponse.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                return isInitialized;
            }
        }

        /// <summary>
        /// Gets the command-line arguments for the driver service.
        /// </summary>
        protected override string CommandLineArguments
        {
            get
            {
                StringBuilder argsBuilder = new StringBuilder();
                if (this.browserCommunicationPort > 0)
                {
                    argsBuilder.AppendFormat(CultureInfo.InvariantCulture, " --marionette-port {0}", this.browserCommunicationPort);
                }

                if (this.Port > 0)
                {
                    argsBuilder.AppendFormat(CultureInfo.InvariantCulture, " --webdriver-port {0}", this.Port);
                }

                if (!string.IsNullOrEmpty(this.browserBinaryPath))
                {
                    argsBuilder.AppendFormat(CultureInfo.InvariantCulture, " --binary \"{0}\"", this.browserBinaryPath);
                }

                return argsBuilder.ToString().Trim();
            }
        }

        /// <summary>
        /// Creates a default instance of the FirefoxDriverService.
        /// </summary>
        /// <returns>A FirefoxDriverService that implements default settings.</returns>
        public static FirefoxDriverService CreateDefaultService()
        {
            string serviceDirectory = DriverService.FindDriverServiceExecutable(FirefoxDriverServiceFileName, FirefoxDriverDownloadUrl);
            return CreateDefaultService(serviceDirectory);
        }

        /// <summary>
        /// Creates a default instance of the FirefoxDriverService using a specified path to the Firefox driver executable.
        /// </summary>
        /// <param name="driverPath">The directory containing the Firefox driver executable.</param>
        /// <returns>A FirefoxDriverService using a random port.</returns>
        public static FirefoxDriverService CreateDefaultService(string driverPath)
        {
            return CreateDefaultService(driverPath, FirefoxDriverServiceFileName);
        }

        /// <summary>
        /// Creates a default instance of the FirefoxDriverService using a specified path to the ChromeDriver executable with the given name.
        /// </summary>
        /// <param name="driverPath">The directory containing the Firefox driver executable.</param>
        /// <param name="driverExecutableFileName">The name of th  Firefox driver executable file.</param>
        /// <returns>A FirefoxDriverService using a random port.</returns>
        public static FirefoxDriverService CreateDefaultService(string driverPath, string driverExecutableFileName)
        {
            return new FirefoxDriverService(driverPath, driverExecutableFileName, PortUtilities.FindFreePort());
        }
    }
}
