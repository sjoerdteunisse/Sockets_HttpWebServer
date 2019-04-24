namespace Sockets_HttpWebserver
{
    class Program
    {
        static void Main(string[] args)
        {
            //Initializes a websiteserver on a designated port.
            HttpWebserver httpWebserver = new HttpWebserver(5000);
        }
    }
}
