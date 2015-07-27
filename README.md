# Rexy
A C# REverse proXY for SOAP requests, over a RabbitMQ message broker. The 
project consists of two main projects, the proxy endpoint 
(`RabbitMQ.Adapters.HttpHandlers`) and the SOAP forwarder 
(`RabbitMQ.Adapters.WebServiceCaller`).

This allows to decouple webserver clients and servers and supports and 
incremental move from an architecture based on webservices to one based on a 
message queue.

## Features
  * Configuration of routes (which call gets forwarded to which server) on the reverse proxy
  * Support for windows authentication
  * Modifying response with wsdl data to use web addresses of the reverse proxy instead of the original SOAP server.
  * Support for gzip-compressed responses

## Overview
Example of a typical setup:
```
                   ReverseProxy     RabbitMQ                                   
 SOAP Client       HTTPHandler         ++     WebServiceCaller      SOAP Server
                                       ||                                      
 +--------+         +--------+         ||        +--------+         +--------+ 
 |        |  SOAP   |        +-------->||        |        |  SOAP   |        | 
 |        +--------->        <---------||        |        +--------->        | 
 |        <---------+        |         ||-------->        <---------+        | 
 |        |         |        |         ||<-------+        |         |        | 
 +--------+         +--------+         ||        +--------+         +--------+ 
                                       ||                                      
                                       ++                                      
```

## Installation Instructions

### Installing Rexy

You will need to install the HttpHandler (webService) and the WebServiceCaller (windows service) seperately. These instructions assume that you already have IIS and RabbitMQ installed.

 * HttpHandler: 
   * Copy binaries to an existing IIS App folder
   * Edit routes.xml and add a route to point to your webservice address.

 * WebServiceCaller:
   * Copy binaries to your server
   * Install as a windows service by running in the command line: ``WebServiceCaller.exe -i`` and start the service from Windows Serives Manager

### Installing the demo Client and Server

 * The demo SOAP client:
   * Copy binaries
   * Edit Helloworld.Client.Config and change the client endpoit address to point to your proxy (HttpHandler).
 * The demo SOAP server:
   * Copy binaries to an existing IIS App folder

### Runnig the demo SOAP client
 
 * To run the client just execute HelloWorld.Client.exe from a command line.

### Logging
 
 * Logs from Demo SOAP client are sent to Console;
 * The logging from the remaining services use log4net and can be configured through the respective configuration file; The defaults are set to write to ``c:\ISA\Logs`` folder.

