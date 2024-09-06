using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Json;
using System.Net;

namespace AbxClient
{
    class Program
    {
        static void Main(string[] args)
        {
            // Establish a TCP connection
            TcpClient client = new TcpClient();
            client.Connect("localhost", 3000);
           
            byte callType = 1; // Stream All Packets
            byte resendSeq = 0; // Not applicable for callType 1

            // Create a byte array to hold the request payload
            byte[] requestPayload = new byte[2];

            // Set the call type and resend sequence number in the request payload
            requestPayload[0] = callType;
            requestPayload[1] = resendSeq;

         


            NetworkStream stream = client.GetStream();
            stream.Write(requestPayload, 0, requestPayload.Length);

            // Receive and parse response payload
            byte[] responsePayload = new byte[1024];
            int bytesRead = client.GetStream().Read(responsePayload, 0, responsePayload.Length);
            List<Packet> packets = new List<Packet>();
            int[] index = new int[1];
            while (bytesRead > 0)
            {
                // Parse packet
                Packet packet = ParsePacket(responsePayload, bytesRead,index);
                packets.Add(packet);
                bytesRead -= 17;//each packet is taking 17 bytes so every time we are dicreasing bytesRead count by 17;

            }

            // Handle missing sequences
            int maxSequence = packets[packets.Count - 1].PacketSequence;
            List<int> missingSequences = GetMissingSequences(packets,maxSequence);
            foreach (int sequence in missingSequences)
            {
                // Send a "Resend Packet" request
                TcpClient client2 = new TcpClient();
                client2.Connect("localhost", 3000);

                //byte calltype2 = (byte)2; // resend packets
                //byte resendseq2 = (byte)sequence; // not applicable for calltype 1

                // create a byte array to hold the request payload
                byte[] resendPayload = new byte[2];

                // set the call type and resend sequence number in the request payload
                resendPayload[0] = 2;
                resendPayload[1] = (byte)sequence;

                NetworkStream stream2 = client2.GetStream();
                stream2.Write(resendPayload, 0, resendPayload.Length);

                byte[] responsePay = new byte[17];
                int bytesRead2 = client2.GetStream().Read(responsePay, 0, responsePay.Length);
                Packet resentPacket = ParsePacket(responsePay, bytesRead2, new int[1]);
                packets.Add(resentPacket);
                client2.Close();
            }
            
            // Generate JSON output
            JsonArray jsonArray = new JsonArray();
            foreach (Packet packet in packets)
            {
                JsonObject jsonObject = new JsonObject();
                jsonObject.Add("symbol", packet.Symbol);
                jsonObject.Add("buySellIndicator", packet.BuySellIndicator);
                jsonObject.Add("quantity", packet.Quantity);
                jsonObject.Add("price", packet.Price);
                jsonObject.Add("packetSequence", packet.PacketSequence);
                jsonArray.Add(jsonObject);
            }
            string jsonOutput = jsonArray.ToString();
            //File.WriteAllText("output.json", jsonOutput);
            Console.WriteLine(jsonOutput);
            client.Close();
        }

        static Packet ParsePacket(byte[] payload, int bytesRead, int[] index)
        {
            // Parse packet fields
            int i = index[0];

            byte[] fourBytes = new byte[4];
            Array.Copy(payload, i, fourBytes, 0, 4);

            string symbol = Encoding.ASCII.GetString(fourBytes);

            char buySellIndicator = (char)payload[i+4];

            int quantity = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(payload, i+5));
            int price = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(payload, i+9));
            int packetSequence = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(payload, i+13));

            index[0] += 17;

            return new Packet(symbol, buySellIndicator, quantity, price, packetSequence);
        }

        static List<int> GetMissingSequences(List<Packet> packets, int max)
        {
            // Identify missing sequences
            List<int> missingSequences = new List<int>();
            List<int> allSequences = new List<int>();
            int missing = 0;
            foreach (Packet packet in packets)
            {
                allSequences.Add(packet.PacketSequence);
            }
            allSequences.Sort();
            for(int i=1;i<max;i++)
            {
                if (i != allSequences[missing])
                    missingSequences.Add(i);
                else
                    missing++;
            }
            return missingSequences;
        }
        }
}
public class Packet
{
    public string Symbol { get; set; }
    public char BuySellIndicator { get; set; }
    public int Quantity { get; set; }
    public int Price { get; set; }
    public int PacketSequence { get; set; }

    public Packet(string symbol, char buySellIndicator, int quantity, int price, int packetSequence)
    {
        Symbol = symbol;
        BuySellIndicator = buySellIndicator;
        Quantity = quantity;
        Price = price;
        PacketSequence = packetSequence;
    }

    public override string ToString()
    {
        return $"Symbol: {Symbol}, Buy/Sell Indicator: {BuySellIndicator}, Quantity: {Quantity}, Price: {Price}, Packet Sequence: {PacketSequence}";
    }
}



