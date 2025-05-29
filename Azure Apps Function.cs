/*
Name : Gaurish Bhatia
Student ID: 222187151
Remarks: This is the code for the Azure apps function, which ingests data from the Azure IoT Hub recieved via the telemetry messages and sends it to update the value on the device twin.
*/

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventGrid;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Azure.Core.Pipeline;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using Azure;

namespace factorytwiningestfunction
{
    public static class Function1
    {
        // Read the Azure Digital Twins instance URL from environment variables
        private static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

        // Reuse a single HttpClient instance across requests for performance
        private static readonly HttpClient singletonHttpClientInstance = new HttpClient();

        [FunctionName("Function1")]
        public static async Task Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
        {
            // Check if the ADT instance URL is set in the environment
            if (string.IsNullOrEmpty(adtInstanceUrl))
            {
                log.LogError("Application setting 'ADT_SERVICE_URL' not set");
                return;
            }

            try
            {
                // Use Managed Identity for authentication to Azure services
                var cred = new ManagedIdentityCredential();

                // Create a Digital Twins client using Managed Identity and custom HTTP transport
                var client = new DigitalTwinsClient(
                    new Uri(adtInstanceUrl),
                    cred,
                    new DigitalTwinsClientOptions
                    {
                        Transport = new HttpClientTransport(singletonHttpClientInstance)
                    });

                log.LogInformation("ADT service client connection created.");
                Console.WriteLine("connection on");

                // Ensure event data is not null
                if (eventGridEvent?.Data != null)
                {
                    log.LogInformation("Event data received: " + eventGridEvent.Data.ToString());

                    // Deserialize event data into a JSON object
                    JObject deviceMessage = JsonConvert.DeserializeObject<JObject>(eventGridEvent.Data.ToString());

                    // Set the device ID for the digital twin (should ideally be extracted from the message or event metadata)
                    string deviceId = "DeviceGaurish2710";

                    if (deviceId == null)
                    {
                        log.LogWarning("Device ID not found in message.");
                        Console.WriteLine("Device ID not found in message.");
                        return;
                    }

                    // Extract the 'body' object from the event payload
                    var body = deviceMessage["body"];
                    if (body == null)
                    {
                        log.LogWarning("Device message body is null.");
                        return;
                    }

                    // Read temperature and humidity values from the message body
                    var temperature = body["Temperature"];
                    var humidity = body["Humidity"];

                    if (temperature == null || humidity == null)
                    {
                        log.LogWarning("Temperature or Humidity property missing in message body.");
                        return;
                    }

                    // Log the telemetry values
                    log.LogInformation($"Device: {deviceId} | Temperature: {temperature} | Humidity: {humidity}");

                    // Create a patch document to update the twin properties
                    var updateTwinData = new JsonPatchDocument();
                    updateTwinData.AppendReplace("/Temperature", temperature.Value<double>());
                    updateTwinData.AppendReplace("/Humidity", humidity.Value<double>());

                    // Update the digital twin with the new telemetry values
                    await client.UpdateDigitalTwinAsync(deviceId, updateTwinData);
                    log.LogInformation($"Digital twin for device '{deviceId}' updated successfully.");
                }
                else
                {
                    log.LogWarning("EventGridEvent or its Data is null.");
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions encountered during execution
                log.LogError($"Error in ingest function: {ex.Message}");
            }
        }
    }
}
