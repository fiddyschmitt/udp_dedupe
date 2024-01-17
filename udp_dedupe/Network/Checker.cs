using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using udp_dedupe.Config;
using WinDivertSharp;
using WinDivertSharp.WinAPI;

namespace udp_dedupe.Network
{
    public class Checker
    {
        public Check Check { get; }

        public Checker(Check check)
        {
            Check = check;
        }

        public void Start()
        {
            var handle = WinDivert.WinDivertOpen(Check.Filter, WinDivertLayer.Network, 0, WinDivertOpenFlags.None);

            var packet = new WinDivertBuffer();
            var addr = new WinDivertAddress();
            var readLen = 0U;

            NativeOverlapped recvOverlapped;

            var recvEvent = IntPtr.Zero;
            var recvAsyncIoLen = 0U;


            while (true)
            {
                readLen = 0;


                recvAsyncIoLen = 0;
                recvOverlapped = new NativeOverlapped();

                recvEvent = Kernel32.CreateEvent(IntPtr.Zero, false, false, IntPtr.Zero);

                if (recvEvent == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to initialize receive IO event.");
                    continue;
                }

                addr.Reset();

                recvOverlapped.EventHandle = recvEvent;

                if (!WinDivert.WinDivertRecvEx(handle, packet, 0, ref addr, ref readLen, ref recvOverlapped))
                {
                    var error = Marshal.GetLastWin32Error();

                    // 997 == ERROR_IO_PENDING
                    if (error != 997)
                    {
                        Console.WriteLine(string.Format("Unknown IO error ID {0} while awaiting overlapped result.", error));
                        Kernel32.CloseHandle(recvEvent);
                        continue;
                    }

                    while (Kernel32.WaitForSingleObject(recvEvent, 1000) == (uint)WaitForSingleObjectResult.WaitTimeout)
                    {

                    }

                    if (!Kernel32.GetOverlappedResult(handle, ref recvOverlapped, ref recvAsyncIoLen, false))
                    {
                        Console.WriteLine("Failed to get overlapped result.");
                        Kernel32.CloseHandle(recvEvent);
                        continue;
                    }

                    readLen = recvAsyncIoLen;
                }

                Kernel32.CloseHandle(recvEvent);

                //Console.WriteLine("Received packet {0}", readLen);

                var parsedPacket = WinDivert.WinDivertHelperParsePacket(packet, readLen);


                //if (parsedPacket.IPv4Header != null && tcpHeader != null)
                //{
                //    Console.WriteLine($"V4 TCP packet {addr.Direction} from {ipv4Header.Value.SrcAddr}:{tcpHeader.Value.SrcPort} to {ipv4Header.Value.DstAddr}:{tcpHeader.Value.DstPort}");
                //}
                //else if (ipv6Header != null && tcpHeader != null)
                //{
                //    Console.WriteLine($"V4 TCP packet {addr.Direction} from {ipv6Header.Value.SrcAddr}:{tcpHeader.Value.SrcPort}  to  {ipv6Header.Value.DstAddr} : {tcpHeader.Value.DstPort}");
                //}

                var payloadArray = new byte[parsedPacket.PacketPayloadLength];
                unsafe
                {
                    Marshal.Copy((IntPtr)parsedPacket.PacketPayload, payloadArray, 0, payloadArray.Length);
                }

                if (payloadArray != null)
                {
                    Console.WriteLine($"{DateTime.Now}: Packet has {payloadArray.Length:N0} byte payload.");
                }

                //Console.WriteLine($"{nameof(addr.Direction)} - {addr.Direction}");
                //Console.WriteLine($"{nameof(addr.Impostor)} - {addr.Impostor}");
                //Console.WriteLine($"{nameof(addr.Loopback)} - {addr.Loopback}");
                //Console.WriteLine($"{nameof(addr.IfIdx)} - {addr.IfIdx}");
                //Console.WriteLine($"{nameof(addr.SubIfIdx)} - {addr.SubIfIdx}");
                //Console.WriteLine($"{nameof(addr.Timestamp)} - {addr.Timestamp}");
                //Console.WriteLine($"{nameof(addr.PseudoIPChecksum)} - {addr.PseudoIPChecksum}");
                //Console.WriteLine($"{nameof(addr.PseudoTCPChecksum)} - {addr.PseudoTCPChecksum}");
                //Console.WriteLine($"{nameof(addr.PseudoUDPChecksum)} - {addr.PseudoUDPChecksum}");

                // Console.WriteLine(WinDivert.WinDivertHelperCalcChecksums(packet, ref addr, WinDivertChecksumHelperParam.All));

                //if (!WinDivert.WinDivertSendEx(handle, packet, readLen, 0, ref addr))
                //{
                //    Console.WriteLine("Write Err: {0}", Marshal.GetLastWin32Error());
                //}
            }
        }

        static void ProcessPacket(WinDivertBuffer packet, WinDivertAddress addr)
        {
            // Your packet processing logic here
            Console.WriteLine("Packet received!");
        }
    }
}