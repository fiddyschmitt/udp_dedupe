using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        public void Start()
        {
            var handle = WinDivert.WinDivertOpen(Check.Filter, WinDivertLayer.Network, 0, WinDivertOpenFlags.None);

            var packet = new WinDivertBuffer();
            var addr = new WinDivertAddress();

            NativeOverlapped recvOverlapped;

            var bytesToInspect = Check.BytesToInspect.ToList();
            var checkEntirePayload = bytesToInspect.Count == 0;
            var minimumLengthPayloadMustBe = bytesToInspect
                                                .OrderBy(b => b)
                                                .LastOrDefault();

            var recentDatagrams = new MemoryCache("RecentDatagrams");

            while (true)
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
                        Console.WriteLine(string.Format("Unknown IO error ID {0} while awaiting overlapped result.", error));
                        Kernel32.CloseHandle(recvEvent);
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
                unsafe
                {
                    Marshal.Copy((IntPtr)parsedPacket.PacketPayload, payloadArray, 0, payloadArray.Length);
                }


                bool? shouldForward = null;

                if (shouldForward == null && checkEntirePayload && payloadArray != null)
                {
                    //check if we've seen the whole packet recently

                    var payloadHex = payloadArray.ToHexString();

                    if (recentDatagrams.Contains(payloadHex))
                    {
                        shouldForward = false;
                        Console.WriteLine($"{DateTime.Now} Duplicate packet detected (based on entire payload). Dropping.");
                    }
                    else
                    {
                        shouldForward = true;
                        recentDatagrams.Set(payloadHex, new object(), DateTimeOffset.UtcNow.AddMilliseconds(Check.TimeWindowInMilliseconds));
                        Console.WriteLine($"{DateTime.Now} Forwarding packet (it has unique content within last {Check.TimeWindowInMilliseconds:N0} ms)");
                    }
                }

                if (shouldForward == null && payloadArray != null)
                {
                    if (payloadArray.Length < minimumLengthPayloadMustBe)
                    {
                        shouldForward = true;
                        Console.WriteLine($"{DateTime.Now} Forwarding packet (it's smaller than the bytes that need to be checked)");
                    }
                    else
                    {
                        //check if we've seen part of the packet recently

                        var subpayloadHex = bytesToInspect
                                                .Select(byteIndex => payloadArray[byteIndex])
                                                .ToArray()
                                                .ToHexString();

                        if (recentDatagrams.Contains(subpayloadHex))
                        {
                            shouldForward = false;
                            Console.WriteLine($"{DateTime.Now} Duplicate packet detected (based on payload subset). Dropping.");
                        }
                        else
                        {
                            shouldForward = true;
                            recentDatagrams.Set(subpayloadHex, new object(), DateTimeOffset.UtcNow.AddMilliseconds(Check.TimeWindowInMilliseconds));
                            Console.WriteLine($"{DateTime.Now} Forwarding packet (it has unique subset content within last {Check.TimeWindowInMilliseconds:N0} ms)");
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
            }
        }
    }
}