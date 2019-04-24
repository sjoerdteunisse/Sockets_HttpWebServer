/*
 * Copyright (c) 2019. All rights reserved.
 * Author: Sjoerd Teunisse
 * Contact details: sjoerdteunisse at googleMailDns server dot com
 */

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Sockets_HttpWebserver
{
    public class HttpWebserver
    {
        private TcpListener _TCPListener;
        private readonly string _sMyWebServerRoot = "C:\\MyWebServerRoot\\";

        /// <summary>
        /// Initialize webserver on given port
        /// http://localhost/[port]/file{get}
        /// </summary>
        /// <param name="port">port to start webserver on</param>
        public HttpWebserver(int port)
        {
            try
            {
                _TCPListener = new TcpListener(port);
                _TCPListener.Start();

                Console.WriteLine("Web Server Running... Press control C to Stop...");

                var th = new Thread(StartListen);
                th.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine("An Exception Occurred while Listening :" + e);
            }
        }

        /// <summary>
        /// Resolve mimetype based on the given file
        /// </summary>
        /// <param name="requestedFile"></param>
        /// <returns></returns>
        public string GetMimeType(string requestedFile)
        {
            return MimeTypeResolver.GetMIMEType(requestedFile);
        }

        /// <summary>
        /// Transfer content to browser
        /// </summary>
        /// <param name="sData"></param>
        /// <param name="mySocket"></param>
        public void SendToBrowser(string sData, ref Socket mySocket)
        {
            SendToBrowser(Encoding.ASCII.GetBytes(sData), ref mySocket);
        }

        /// <summary>
        /// Coherent header that is transferred to client
        /// </summary>
        /// <param name="sHttpVersion">httpVer</param>
        /// <param name="sMimeHeader">MimeType</param>
        /// <param name="totalBytes">TotalBytes</param>
        /// <param name="statusCode"></param>
        /// <param name="mySocket"></param>
        public void SendHeader(string sHttpVersion, string sMimeHeader, int totalBytes, string statusCode, ref Socket mySocket)
        {
            var stringBuffer = "";

            // if Mime type is not provided set default to text/html  
            if (sMimeHeader.Length == 0)
            {
                sMimeHeader = "text/html";// Default Mime Type is text/html  
            }

            //Closing tags: \carriage\new line
            stringBuffer += stringBuffer + sHttpVersion + statusCode + "\r\n";
            stringBuffer += "Server: custom\r\n";
            stringBuffer += "Content-Type: " + sMimeHeader + "\r\n";
            stringBuffer += "Accept-Ranges: bytes\r\n";
            stringBuffer += "Content-Length: " + totalBytes + "\r\n\r\n";

            var bSendData = Encoding.ASCII.GetBytes(stringBuffer);
            SendToBrowser(bSendData, ref mySocket);
            Console.WriteLine("Total Bytes : " + totalBytes.ToString());
        }

        /// <summary>
        /// Transfer bytes to client with use of tcp socket
        /// </summary>
        /// <param name="sendData">Data to send as byte[]</param>
        /// <param name="mySocket">ref of socket used</param>
        public void SendToBrowser(byte[] sendData, ref Socket mySocket)
        {
            try
            {
                if (mySocket.Connected)
                {
                    int numBytes;

                    if ((numBytes = mySocket.Send(sendData, sendData.Length, 0)) == -1)
                        Console.WriteLine("Socket Error occured cannot Send Packet");
                    else
                    {
                        Console.WriteLine("Number of bytes transferred {0}", numBytes);
                    }
                }
                else Console.WriteLine("Connection Dropped....");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Occurred : {0} ", e);
            }
        }

        /// <summary>
        /// OnRequest listen; get uri;
        /// get requested file
        /// check if file exists
        /// transfer bytes from file to byte[]
        /// 
        /// finally transfer to client
        ///     -headers
        ///     -content
        /// </summary>
        public void StartListen()
        {
            var sResponse = "";
            var sErrorMessage = "";
            var sFormattedMessage = "";
            
            while (true)
            {
                //Accept a new connection  
                Socket mySocket = _TCPListener.AcceptSocket();
                Console.WriteLine("Socket Type " + mySocket.SocketType);

                if (!mySocket.Connected)
                    continue;

                Console.WriteLine("\nClient connected\n==================\n  CLient IP {0}\n", mySocket.RemoteEndPoint) ;  
                    
                //make a byte array and receive data from the client   
                var bReceive = new byte[1024];
                    
                //byRef
                var receive = mySocket.Receive(bReceive, bReceive.Length, 0);
                    
                //Convert Byte to String  
                var sBuffer = Encoding.ASCII.GetString(bReceive);

                //Only check for GET
                if (sBuffer.Substring(0, 3) != "GET")
                {
                    Console.WriteLine("Only Get Method is supported..");
                    mySocket.Close();
                    return;
                }

                var startPos = sBuffer.IndexOf("HTTP", 1, StringComparison.Ordinal);
                var sHttpVersion = sBuffer.Substring(startPos, 8);
                    
                //File request
                var sRequest = sBuffer.Substring(0, startPos - 1);

                sRequest.Replace("\\", "/");


                if (sRequest.IndexOf(".", StringComparison.Ordinal) < 1 && (!sRequest.EndsWith("/")))
                {
                    sRequest = sRequest + "/";
                }

                startPos = sRequest.LastIndexOf("/", StringComparison.Ordinal) + 1;

                var sRequestedFile = sRequest.Substring(startPos);
                    
                var sDirName = sRequest.Substring(sRequest.IndexOf("/", StringComparison.Ordinal), sRequest.LastIndexOf("/", StringComparison.Ordinal) - 3);
                    
                var sLocalDir = sDirName == "/" ? _sMyWebServerRoot : "";

                Console.WriteLine("Directory Requested : " + sLocalDir);

                if (sLocalDir.Length == 0)
                {
                    //set error message
                    sErrorMessage = "<H2>Error!! Requested Directory does not exists</H2><Br>";

                    //Append header
                    SendHeader(sHttpVersion, "", sErrorMessage.Length, " 404 Not Found", ref mySocket);

                    //Send content
                    SendToBrowser(sErrorMessage, ref mySocket);

                    //Close socket
                    mySocket.Close();
                    continue;
                }

                //get mimetype
                var sMimeType = GetMimeType(sRequestedFile);
                    
                //Build the physical path  
                var sPhysicalFilePath = sLocalDir + sRequestedFile;
                //Debug: write file location
                Console.WriteLine("File Requested : " + sPhysicalFilePath);

                TransferToClient(sPhysicalFilePath, sErrorMessage, ref mySocket, sHttpVersion, sFormattedMessage, sResponse, sMimeType);
            }
        }

        /// <summary>
        /// Checks if the file exists, and handles the final transferring to client.
        /// 
        /// </summary>
        /// <param name="physicalFilePath"></param>
        /// <param name="errorMessage"></param>
        /// <param name="mySocket"></param>
        /// <param name="sHttpVersion"></param>
        /// <param name="formattedMessage"></param>
        /// <param name="response"></param>
        /// <param name="mimeType"></param>
        private void TransferToClient(string physicalFilePath, string errorMessage, ref Socket mySocket, string sHttpVersion, string formattedMessage, string response, string mimeType)
        {
            if (!File.Exists(physicalFilePath))
            {
                errorMessage = "<H2>404 Error: File Does Not Exists...</H2>";
                SendHeader(sHttpVersion, "", errorMessage.Length, " 404 Not Found", ref mySocket);
                SendToBrowser(errorMessage, ref mySocket);

                Console.WriteLine(formattedMessage);
            }
            else
            {
                var totalBytes = 0;
                response = "";

                //Create file stream on file;
                var fs = new FileStream(physicalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                
                // Create a reader that can read bytes from the FileStream.  
                var reader = new BinaryReader(fs);

                var bytes = new byte[fs.Length];

                int read;
                while ((read = reader.Read(bytes, 0, bytes.Length)) != 0)
                {
                    // Read from the file and write the string to send to the browser.
                    response = response + Encoding.ASCII.GetString(bytes, 0, read);
                    totalBytes += read;
                }
                reader.Close();
                fs.Close();

                SendHeader(sHttpVersion, mimeType, totalBytes, " 200 OK", ref mySocket);

                SendToBrowser(bytes, ref mySocket);
            }

            //Clean up, close socket.
            mySocket.Close();
        }
    }
}
