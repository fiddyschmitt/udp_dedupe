# UDP Dedupe

This program inspects network packets and drops any which are duplicates.

# Download
Download from the [releases](https://github.com/fiddyschmitt/udp_dedupe/releases) section.

# Usage

Edit `settings.json`.
The `Filter` specifies what packets to inspect.

<br/>

### Example 1 - Inspect all UDP packets
```
{
  "Checks": [
    {
      "Filter": "udp",
      "TimeWindowInMilliseconds": 5000
    }
  ]
}
```

<br/>

### Example 2 - Inspect incoming IPV4 UDP packets with destination port 15000
```
{
  "Checks": [
    {
      "Filter": "inbound && !ipv6 && udp && udp.DstPort == 15000",
      "TimeWindowInMilliseconds": 5000
    }
  ]
}
```


<br/>

More info about the WinDivert Filter Language [here](https://www.reqrypt.org/windivert-doc.html#filter_language).

<br/>

### Run the program
`udp_dedupe.exe`

# How does it work?
The program uses the [WinDivert](https://github.com/basil00/Divert) library to capture the packets specified in the filter. If it sees the same payload in a certain window of time, it will drop the packet.
