/*
 * IoT Hub Raspberry Pi NodeJS - Microsoft Sample Code - Copyright (c) 2017 - Licensed MIT

 REMARKS: Addition of changes to the original microsoft code being undertaken in order to implement the device messages as per the payload.
 */

// Import required modules
const wpi = require('wiring-pi'); // For controlling GPIO on Raspberry Pi
const Client = require('azure-iot-device').Client; // Azure IoT client
const Message = require('azure-iot-device').Message; // Azure IoT message constructor
const Protocol = require('azure-iot-device-mqtt').Mqtt; // MQTT protocol for IoT Hub
const BME280 = require('bme280-sensor'); // BME280 sensor module

// BME280 sensor configuration
const BME280_OPTION = {
  i2cBusNo: 1, // I2C bus number (usually 1 for Raspberry Pi)
  i2cAddress: BME280.BME280_DEFAULT_I2C_ADDRESS() // Default I2C address (0x77)
};

// Azure IoT Hub connection string
const connectionString = 'HostName=SiT724DigitalTwinsIoTHub.azure-devices.net;DeviceId=DeviceGaurish2710;SharedAccessKey=1t/EuifHC81FCkOdlUotILRrmprL7hbm1jMhuzESsRs=';

// GPIO pin connected to LED
const LEDPin = 4;

// Variables for tracking sensor and messaging
var sendingMessage = false;
var messageId = 0;
var client, sensor;
var blinkLEDTimeout = null;

// Function to read sensor data and prepare message
function getMessage(cb) {
  messageId++;
  sensor.readSensorData()
    .then(function (data) {
      cb(JSON.stringify({
        Temperature: data.temperature_C, // Temperature in Celsius
        Humidity: data.humidity // Relative humidity in %
      }), data.temperature_C > 30); // Set alert if temp > 30°C
    })
    .catch(function (err) {
      console.error('Failed to read out sensor data: ' + err);
    });
}

// Function to send message to Azure IoT Hub
function sendMessage() {
  if (!sendingMessage) { return; }

  getMessage(function (content, temperatureAlert) {
    var message = new Message(content);
    message.properties.add('temperatureAlert', temperatureAlert.toString()); // Custom property
    console.log('Sending message: ' + content);

    client.sendEvent(message, function (err) {
      if (err) {
        console.error('Failed to send message to Azure IoT Hub');
      } else {
        blinkLED(); // Indicate successful send
        console.log('Message sent to Azure IoT Hub');
      }
    });
  });
}

// Device method to start telemetry
function onStart(request, response) {
  console.log('Try to invoke method start(' + request.payload + ')');
  sendingMessage = true;

  response.send(200, 'Successfully started sending messages to cloud', function (err) {
    if (err) {
      console.error('[IoT hub Client] Failed sending method response:\n' + err.message);
    }
  });
}

// Device method to stop telemetry
function onStop(request, response) {
  console.log('Try to invoke method stop(' + request.payload + ')');
  sendingMessage = false;

  response.send(200, 'Successfully stopped sending messages to cloud', function (err) {
    if (err) {
      console.error('[IoT hub Client] Failed sending method response:\n' + err.message);
    }
  });
}

// Callback for cloud-to-device (C2D) messages
function receiveMessageCallback(msg) {
  blinkLED(); // Blink LED on message receipt
  var message = msg.getData().toString('utf-8');
  client.complete(msg, function () {
    console.log('Received message: ' + message);
  });
}

// Blink the LED to show activity (500ms)
function blinkLED() {
  if (blinkLEDTimeout) {
    clearTimeout(blinkLEDTimeout);
  }
  wpi.digitalWrite(LEDPin, 1); // Turn LED on
  blinkLEDTimeout = setTimeout(function () {
    wpi.digitalWrite(LEDPin, 0); // Turn LED off
  }, 500);
}

// Initialize GPIO and sensor
wpi.setup('wpi'); // Setup wiringPi
wpi.pinMode(LEDPin, wpi.OUTPUT); // Set LED pin as output
sensor = new BME280(BME280_OPTION);
sensor.init()
  .then(function () {
    sendingMessage = true; // Enable telemetry after sensor initializes
  })
  .catch(function (err) {
    console.error(err.message || err);
  });

// Create Azure IoT Hub client using MQTT
client = Client.fromConnectionString(connectionString, Protocol);

// Connect to Azure IoT Hub and set up callbacks
client.open(function (err) {
  if (err) {
    console.error('[IoT hub Client] Connect error: ' + err.message);
    return;
  }

  // Register method and message handlers
  client.onDeviceMethod('start', onStart); // Handle 'start' method from cloud
  client.onDeviceMethod('stop', onStop);   // Handle 'stop' method from cloud
  client.on('message', receiveMessageCallback); // Handle cloud-to-device messages

  // Periodically send telemetry every 5 seconds
  setInterval(sendMessage, 5000);
});
