# Sample MassTransit .NET Core Service

In this sample, a .NET core service is created (using .NET Core 3.1) which supports logging, configuration, and dependency injection, along with a basic MassTransit configuration to connect to RabbitMQ.

In the past, this would have been accomplished using Topshelf, however, at this point Topshelf is really specific to Windows and isn't needed with .NET Core to create Linux/Mac services.

## Building

To build the sample, just go into the folder and type `dotnet run`. It will attempt to connect to the broker based upon the configuration in the `appsettings.json` file.