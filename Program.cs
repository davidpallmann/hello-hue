namespace HelloHue
{
    using Amazon;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Microsoft.Extensions.Configuration;

    public class Program
    {
        static string BaseURL = null!;
        static string Username = null!;
        static RegionEndpoint Region = RegionEndpoint.USWest2;
        static string QueueName = null!;
        static HttpClient Client = null!;

        static async Task Main(string[] args)
        {
            // Load Philips API Base URL and Username from settings.

            var builder = new ConfigurationBuilder().AddJsonFile($"appsettings.json", true, true);
            var config = builder.Build();
            BaseURL = config["hue:baseUrl"];
            Username = config["hue:username"];
            QueueName = config["hue:queue"];

            if (args.Length < 1 || args[0] == "-h")
            {
                Console.WriteLine("Use this command to control your Philips Hue lights.");
                Console.WriteLine("Get the state of a light ................  dotnet run -- <light#> state");
                Console.WriteLine("Turn light ON ...........................  dotnet run -- <light#> on");
                Console.WriteLine("Turn light OFF ..........................  dotnet run -- <light#> off");
                Console.WriteLine("Alert on light for 15 seconds ...........  dotnet run -- <light#> alert");
                Console.WriteLine("Set light color (using color name) ......  dotnet run -- <light#> color red|orange|yellow|green|blue|purple|white");
                Console.WriteLine("Set light hue, brightness, saturation .... dotnet run -- <light#> hbs <hue 0..65280> <brightness 0..254> <saturation 25..200>");
                Console.WriteLine("Monitor AWS queue for light commands ..... dotnet run -- queue");
                Environment.Exit(0);
            }

            Client = new HttpClient();
            Client.BaseAddress = new Uri(BaseURL);

            int hue, bri, sat;
            HttpResponseMessage response;

            var ID = args[0];

            var command = args.Length > 1 ? args[1] : args[0];

            switch (command)
            {
                // Get the state of a light.
                // syntax:  dotnet run -- <light#> state
                // example: dotnet run -- 1 state
                case "state":
                    Console.WriteLine($"Getting state of Light {ID}");
                    response = await SendAPICommand("GET", $"/api/{Username}/lights/{ID}");
                    if (response.Content != null)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(responseContent);
                    }
                    break;
                // Turn a light on.
                // syntax:  dotnet run -- <light#> on
                // example: dotnet run -- 1 on
                case "on":
                    Console.WriteLine($"Turning on Light {ID}");
                    response = await SendAPICommand("PUT", $"/api/{Username}/lights/{ID}/state", @"{""on"":true}");
                    break;
                // Turn a light off.
                // syntax:  dotnet run -- <light#> off
                // example: dotnet run -- 1 off
                case "off":
                    Console.WriteLine($"Turning off Light {ID}");
                    response = await SendAPICommand("PUT", $"/api/{Username}/lights/{ID}/state", @"{""on"":false}");
                    break;
                // Perform an alert on a light.
                // syntax:  dotnet run -- <light#> alert
                // example: dotnet run -- 1 alert
                case "alert":
                    Console.WriteLine($"Alerting on Light {ID}");
                    response = await SendAPICommand("PUT", $"/api/{Username}/lights/{ID}/state", @"{""alert"": ""lselect"" }");
                    break;
                // Set a light to a color name.
                // syntax:  dotnet run -- <light#> color red|orange|yellow|green|blue|purple|white
                // example: dotnet run -- 1 blue
                case "color":
                    var color = args[2];
                    hue = 0;
                    bri = 56;
                    sat = 254;
                    Console.WriteLine($"Setting hue-brightness-saturation for color name {color}");
                    switch (color)
                    {
                        case "red":
                            hue = 64634;
                            break;
                        case "purple":
                            hue = 49041;
                            break;
                        case "blue":
                            hue = 44076;
                            break;
                        case "green":
                            hue = 29127;
                             break;
                        case "orange":
                            hue = 4835;
                               break;
                        case "yellow":
                            hue = 10152;
                            break;
                        case "white":
                            hue = 41479;
                            bri = 100;
                            sat = 100;
                            break;
                        default:
                            Console.WriteLine($"Unknown color name: {color}");
                            Environment.Exit(0);
                            break;
                    }
                    Console.WriteLine($"Setting Light {ID} hue-brightness-saturation to {hue} {bri} {sat}");
                    response = await SendAPICommand("PUT", $"/api/{Username}/lights/{ID}/state", $@"{{ ""on"": true, ""bri"": {bri}, ""hue"": {hue}, ""sat"": {sat} }}");
                    break;
                // Set a light hue, brightness, and saturation.
                // syntax:  dotnet run -- <ligh#> hbs <hue 0..65535> <brightness 0..254> <saturation 25..254>
                // example: dotnet run -- 1 hbs 65280 254 200
                case "hbs":
                    hue = Convert.ToInt32(args[2]);
                    bri = Convert.ToInt32(args[3]);
                    sat = Convert.ToInt32(args[4]);
                    Console.WriteLine($"Setting Light {ID} hue-brightness-saturation to {hue} {bri} {sat}");
                    response = await SendAPICommand("PUT", $"/api/{Username}/lights/{ID}/state", $@"{{ ""on"": true, ""bri"": {bri}, ""hue"": {hue}, ""sat"": {sat} }}");
                    break;
                // Monitor AWS queue and process messages by sending commands to lights.
                case "queue":
                    var queueClient = new AmazonSQSClient(Region);

                    var getQueueUrlResponse = await queueClient.GetQueueUrlAsync(QueueName);
                    var queueUrl = getQueueUrlResponse.QueueUrl;

                    Console.WriteLine($"Monitoring AWS queue {queueUrl} for light commands - Ctrl-C to stop");
                    Console.WriteLine();

                    var request = new ReceiveMessageRequest()
                    {
                        QueueUrl = queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 10
                    };

                    while (true)
                    {
                        var queueResponse = await queueClient.ReceiveMessageAsync(request);
                        if (queueResponse.HttpStatusCode == System.Net.HttpStatusCode.OK)
                        {
                            var messages = queueResponse.Messages;
                            if (messages.Count > 0)
                            {
                                 foreach (var msg in messages)
                                {
                                    try
                                    {
                                        var lightCommand = msg.Body;
                                        if (lightCommand != null)
                                        {
                                            var parameters = lightCommand.Split('|');
                                            var action = parameters[0];
                                            var path = parameters[1].Replace("username", Username);
                                            var body = (parameters.Length > 2) ? parameters[2] : String.Empty;
                                            Console.WriteLine($"Message: {lightCommand}");
                                            Console.WriteLine($"Sending light command {action} {path} {body}");
                                            response = await SendAPICommand(action, path, body);
                                            Console.WriteLine();
                                        }
                                    }
                                    catch (InvalidOperationException ex)
                                    {
                                        Console.WriteLine($"Exception deserializing message: {ex.ToString()}");
                                    }
                                }

                                // Delete queue messages
                                foreach (var msg in messages)
                                {
                                    await queueClient.DeleteMessageAsync(queueUrl, msg.ReceiptHandle);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"HTTP status {queueResponse.HttpStatusCode}");
                            break;
                        }
                        Thread.Sleep(5*1000);
                    }
                    break;
                default:
                    Console.WriteLine("Unrecognized command. Type dotnet run -h for help.");
                    break;
            }
        }

        /// <summary>
        /// Send an HTTP action to API, return response
        /// </summary>
        /// <param name="action"></param>
        /// <param name="path"></param>
        /// <param name="request"></param>
        /// <returns>HttpResponseMessage response</returns>
        private static async Task<HttpResponseMessage> SendAPICommand(string action, string path, string request = null!)
        {
            Console.WriteLine($"{action} {path}");
            Console.WriteLine(request);
            HttpResponseMessage response = null!;
            switch(action)
            {
                case "GET":
                    response = await Client.GetAsync(path);
                    break;
                case "PUT":
                    response = await Client.PutAsync(path, new StringContent(request));
                    break;
                case "POST":
                    response = await Client.PostAsync(path, new StringContent(request));
                    break;
            }
            Console.WriteLine(response.StatusCode);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine(response);
            }
            return response;
        }
    }
}
