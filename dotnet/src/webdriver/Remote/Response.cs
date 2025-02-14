﻿// <copyright file="Response.cs" company="WebDriver Committers">
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

using System.Globalization;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;

namespace OpenQA.Selenium.Remote
{
    /// <summary>
    /// Handles reponses from the browser
    /// </summary>
    public class Response
    {
        private object responseValue;
        private string responseSessionId;
        private WebDriverResult responseStatus;
        private bool isSpecificationCompliant;

        /// <summary>
        /// Initializes a new instance of the Response class
        /// </summary>
        public Response()
        {
        }

        /// <summary>
        /// Initializes a new instance of the Response class
        /// </summary>
        /// <param name="sessionId">Session ID in use</param>
        public Response(SessionId sessionId)
        {
            if (sessionId != null)
            {
                this.responseSessionId = sessionId.ToString();
            }
        }

        private Response(Dictionary<string, object> rawResponse)
        {
            if (rawResponse.ContainsKey("sessionId"))
            {
                if (rawResponse["sessionId"] != null)
                {
                    this.responseSessionId = rawResponse["sessionId"].ToString();
                }
            }

            if (rawResponse.ContainsKey("value"))
            {
                this.responseValue = rawResponse["value"];
            }

            if (rawResponse.ContainsKey("status"))
            {
                this.responseStatus = (WebDriverResult)Convert.ToInt32(rawResponse["status"], CultureInfo.InvariantCulture);
            }
            else
            {
                // If the response does *not* have a "status" property, it
                // is compliant with the specification, which does not put
                // status in its responses.
                this.isSpecificationCompliant = true;

                // If the returned object does *not* have a "value" property
                // the response value should be the entirety of the response.
                if (!rawResponse.ContainsKey("value") && this.responseValue == null)
                {
                    // Special-case for the new session command, where the "capabilities"
                    // property of the response is the actual value we're interested in.
                    if (rawResponse.ContainsKey("capabilities"))
                    {
                        this.responseValue = rawResponse["capabilities"];
                    }
                    else
                    {
                        this.responseValue = rawResponse;
                    }
                }

                // Check for an error response by looking for an "error" property,
                // and if found, convert to a numeric status code.
                if (rawResponse.ContainsKey("error"))
                {
                    this.responseStatus = WebDriverError.ResultFromError(rawResponse["error"].ToString());
                }
            }
        }

        /// <summary>
        /// Gets or sets the value from JSON.
        /// </summary>
        public object Value
        {
            get { return this.responseValue; }
            set { this.responseValue = value; }
        }

        /// <summary>
        /// Gets or sets the session ID.
        /// </summary>
        public string SessionId
        {
            get { return this.responseSessionId; }
            set { this.responseSessionId = value; }
        }

        /// <summary>
        /// Gets or sets the status value of the response.
        /// </summary>
        public WebDriverResult Status
        {
            get { return this.responseStatus; }
            set { this.responseStatus = value; }
        }

        /// <summary>
        /// Gets a value indicating whether this response is compliant with the WebDriver specification.
        /// </summary>
        public bool IsSpecificationCompliant
        {
            get { return this.isSpecificationCompliant; }
        }

        /// <summary>
        /// Returns a new <see cref="Response"/> from a JSON-encoded string.
        /// </summary>
        /// <param name="value">The JSON string to deserialize into a <see cref="Response"/>.</param>
        /// <returns>A <see cref="Response"/> object described by the JSON string.</returns>
        public static Response FromJson(string value)
        {
            Dictionary<string, object> deserializedResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(value, new ResponseValueJsonConverter());
            Response response = new Response(deserializedResponse);
            return response;
        }

        /// <summary>
        /// Returns this object as a JSON-encoded string.
        /// </summary>
        /// <returns>A JSON-encoded string representing this <see cref="Response"/> object.</returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Returns the object as a string.
        /// </summary>
        /// <returns>A string with the Session ID, status value, and the value from JSON.</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0} {1}: {2})", this.SessionId, this.Status, this.Value);
        }
    }
}
