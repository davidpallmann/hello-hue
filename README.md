# Hello, Hue - Philips API and .NET

This is the code project for the [AWS & IoT - Philips Hue Lights, Part 2: The API](https://hashnode.com/edit/cl7wgu3jc09zvx9nvccyl1r2s) blog post. 

This is Part 2 on getting the Philips Hue Starter Kit set up for .NET on AWS programatic control. In [Part 1](https://davidpallmann.hashnode.dev/aws-iot-philips-hue-lights-part-1-home-setup), we explained Internet of Things (IoT) and covered home connection of Philips Hue Bridge and Lights. Here in Part 2, we'll set up and learn about the API. This post assumes you have your bridge and lights installed and working with the mobile Hue App.

## Our Hello Hue Project

Now that we have a way to interact with the Hue lights programaticaly, let's write some .NET code to call the API. In this project you'll create a .NET console program to control the Hue lights. 

Afterward, we'll connect this to an AWS SQS queue so we can send commands to the lights  external to the home.

