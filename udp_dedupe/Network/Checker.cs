using System;
using System.Runtime.Caching;
using System.Runtime.InteropServices;
using System.Threading;
using udp_dedupe.Config;
using udp_dedupe.Utilities;
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

        public unsafe void Start()
        {
            var handle = WinDivert.WinDivertOpen(Check.Filter, WinDivertLayer.Network, 0, WinDivertOpenFlags.None);

            var packet = new WinDivertBuffer();
            var addr = new WinDivertAddress();

            NativeOverlapped recvOverlapped;

            var recentDatagrams = new MemoryCache("RecentDatagrams");

            while (true)
            {
                try
                {
                    recvOverlapped = new NativeOverlapped();

                    IntPtr recvEvent = Kernel32.CreateEvent(IntPtr.Zero, false, false, IntPtr.Zero);

                    if (recvEvent == IntPtr.Zero)
                    {
                        Console.WriteLine("Failed to initialize receive IO event.");
                        continue;
                    }

                    addr.Reset();

                    recvOverlapped.EventHandle = recvEvent;

                    var readLen = 0U;
                    if (!WinDivert.WinDivertRecvEx(handle, packet, 0, ref addr, ref readLen, ref recvOverlapped))
                    {
                        var error = Marshal.GetLastWin32Error();

                        // 997 == ERROR_IO_PENDING
                        if (error != 997)
                        {
                            Console.WriteLine($"Unknown IO error ID {error} returned by WinDivert.WinDivertRecvEx.");
                            Console.WriteLine("Please run the process with Administrative privileges.");
                            Kernel32.CloseHandle(recvEvent);
                            Environment.Exit(1);

                            continue;
                        }

                        while (Kernel32.WaitForSingleObject(recvEvent, 1000) == (uint)WaitForSingleObjectResult.WaitTimeout)
                        {

                        }

                        var recvAsyncIoLen = 0U;
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
                    Marshal.Copy((IntPtr)parsedPacket.PacketPayload, payloadArray, 0, payloadArray.Length);

                    bool? shouldForward = null;

                    if (payloadArray != null)
                    {
                        //check if we've seen the packet recently

                        var payloadHex = payloadArray.ToHexString();

                        var ipSrc = (parsedPacket.IPv4Header != null ? parsedPacket.IPv4Header->SrcAddr : null);
                        ipSrc ??= (parsedPacket.IPv6Header != null ? parsedPacket.IPv6Header->SrcAddr : null);

                        var ipDst = (parsedPacket.IPv4Header != null ? parsedPacket.IPv4Header->DstAddr : null);
                        ipDst ??= (parsedPacket.IPv6Header != null ? parsedPacket.IPv6Header->DstAddr : null);

                        var packetStr = $"[{addr.Direction}] [{ipSrc}:{parsedPacket.UdpHeader->SrcPort} -> {ipDst}:{parsedPacket.UdpHeader->DstPort}] [{parsedPacket.PacketPayloadLength:N0} bytes payload]";

                        if (recentDatagrams.Contains(payloadHex))
                        {
                            shouldForward = false;
                            Console.WriteLine($"{DateTime.Now} {packetStr} Dropping duplicate packet.");
                        }
                        else
                        {
                            shouldForward = true;
                            recentDatagrams.Set(payloadHex, new object(), DateTimeOffset.UtcNow.AddMilliseconds(Check.TimeWindowInMilliseconds));
                            //Console.WriteLine($"{DateTime.Now} {packetStr} Packet is unique. Forwarding.");
                        }
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

                        //still undecided if it should be forwarded. Let's forward it by default
                    shouldForward ??= true;

                    if (shouldForward.Value)
                    {
                        var forwardedSuccessfully = WinDivert.WinDivertSendEx(handle, packet, readLen, 0, ref addr);
                        if (forwardedSuccessfully)
                        {

                        }
                        else
                        {
                            Console.WriteLine($"Unable to forward packet: {Marshal.GetLastWin32Error()}");
                        }
                    }
                    else
                    {

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while processing packet: {ex}");
                }
            }
        }
    }
}
