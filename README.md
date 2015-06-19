# Rexy
A C# reverse proxy for SOAP requests, over a RabbitMQ message broker. The 
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

