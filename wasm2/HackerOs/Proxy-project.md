---

## System Description for AI Prompt

You are tasked with outlining the design and features for a **local proxy server application with a graphical user interface (GUI)**. This server's primary purpose is to enable a **WebAssembly-based OS simulator**, built with Blazor and C#, to interact with the host system's real network capabilities and file system.

Currently, the OS simulator operates within the sandboxed environment of a web browser. It features an **emulated `TcpSocket` class** for network operations and utilizes **IndexedDB** for its internal file system storage. The core objective is to allow this OS simulator, when hosted and running on `localhost`, to connect to the proxy server (also running on `localhost`). This connection will facilitate two main functionalities:

1.  Establish **real TCP connections** to the internet or other network services, moving beyond its current network emulation.
2.  **Mount and access specific folders from the host machine's file system** from within the OS simulator. This should integrate with its existing IndexedDB-based file system. Information regarding these mounts can be stored in a hidden file within the shared host folder itself.

A key requirement is that the OS simulator must be able to **connect to and utilize multiple proxy server instances** simultaneously, allowing for flexible configurations for network access and file system resources.

---

## Required Features for the Proxy Server System

### I. Proxy Server Application (Core Backend)

* **Development:** Create a desktop application that functions as the proxy server.
* **Communication Protocol with WebAssembly OS Simulator:**
    * Define and implement a clear, efficient, and robust protocol for communication between the WebAssembly OS simulator and the proxy server (e.g., WebSockets, HTTP/S API, gRPC-web). This communication will occur over `localhost`.
* **TCP Proxying Module:**
    * Listen for incoming connection requests from the OS simulator.
    * Establish outgoing TCP connections to the internet or specified network targets based on the simulator's requests.
    * Relay data bidirectionally between the simulator's `TcpSocket` (once modified) and the actual network sockets on the host.
    * Handle multiple concurrent TCP connections efficiently.
    * Manage the lifecycle of TCP connections (creation, data transfer, termination, error handling).
* **File System Access Module (Host Side):**
    * Enable configuration of specific host system folders to be "exposed" or "shared" with the OS simulator.
    * Provide an API that allows the OS simulator to:
        * Discover available shared folders from a connected proxy instance.
        * Request to "mount" an exposed host folder into its own file system.
        * Perform file system operations (e.g., read, write, delete, list files/directories, retrieve metadata) on these mounted folders.
    * Support the use of a hidden file (e.g., `.mount_info`, `.folder_config`) within the shared host folder to store metadata or mounting configuration, if necessary.
    * Implement security measures to strictly limit file system access to only the explicitly shared/exposed folders.
* **Instance Awareness:**
    * The proxy server application should run as a distinct instance. The overall system must support the OS simulator connecting to multiple such proxy instances.

### II. Graphical User Interface (GUI) for the Proxy Server Application

* **Status Monitoring:**
    * Display the current operational status of the proxy server (e.g., running, stopped, listening port, errors).
    * List any connected OS simulator instances or sessions.
    * Show active TCP connections being proxied (e.g., simulator source, actual destination, basic traffic statistics).
    * List currently shared host folders and their mount aliases/paths.
* **Configuration Management:**
    * Allow users to configure the listening IP address (typically `127.0.0.1`) and port for the proxy server.
    * Manage shared host folders:
        * Interface to add new host folders to be shared.
        * Input for the full path to the host folder.
        * Option to define an alias or mount point name for the OS simulator.
        * Ability to remove shared folders from the configuration.
        * (Optional) Set access permissions (e.g., read-only, read-write) on a per-folder basis.
* **Activity Logging:**
    * Display real-time logs of significant events, such as new TCP connections, connection terminations, file access requests (and their success/failure), and errors.
    * Option to export or save activity logs to a file.

### III. OS Simulator Integration (WebAssembly Client-Side Modifications)

* **Network Stack (`TcpSocket` Class) Enhancement:**
    * Modify the existing `TcpSocket` emulation layer within the WebAssembly OS simulator.
    * Instead of purely emulating network behavior, the class should:
        * Connect to one or more configured proxy server instances using the defined communication protocol.
        * Transmit TCP connection requests (including target host and port) to the designated proxy.
        * Send and receive TCP data payloads through the proxy.
        * Properly handle connection status updates, data, and errors as relayed by the proxy server.
* **File System Interface Enhancement (IndexedDB Integration):**
    * Extend the current IndexedDB-based file system implementation.
    * Introduce logic to recognize and handle "mount points" that correspond to folders shared by a proxy server.
    * When the OS simulator attempts to access a path that falls under such a mount point:
        * Translate the OS simulator's internal file system calls (e.g., `openFile`, `readFile`, `writeFile`, `listDirectoryContents`) into API requests directed to the appropriate proxy server managing that shared folder.
        * Seamlessly integrate the responses (file data, directory listings, error codes) from the proxy server into the OS simulator's file system view.
    * Develop a mechanism within the OS simulator to discover or be configured with mount point information (e.g., reading from user settings within the OS, or by querying connected proxies for available shares).
* **Multi-Proxy Connectivity and Management:**
    * Implement a system within the OS simulator to manage connections to multiple proxy server instances.
    * Allow users to configure connection details for several proxy servers (e.g., `localhost:7001`, `localhost:7002`, potentially even remote proxies if design evolves).
    * Enable routing of network requests to a specific proxy based on rules or default settings within the OS simulator (e.g., one proxy for general internet, another for specific internal resources).
    * Ensure file system operations on mounted folders are directed to the correct proxy instance that is sharing that specific folder.
    * Provide a user interface or configuration mechanism within the OS simulator for:
        * Adding, editing, and removing proxy server connection profiles.
        * Selecting default proxy servers for different services (e.g., networking, file system access).
        * Browse and initiating the mounting of available shared folders from any connected and configured proxy server.

### IV. General System Requirements

* **Security:**
    * Prioritize secure communication between the WebAssembly application and the local proxy server.
    * Clearly define and enforce boundaries for file system access granted by the proxy to the host machine, preventing unauthorized access.
* **Error Handling and Resilience:**
    * Implement comprehensive error handling for scenarios such as network disruptions, file access permission issues, and proxy server unavailability or crashes.
    * The system should be resilient and provide clear feedback to the user in case of errors.
* **Performance:**
    * Design for efficient data transfer for both TCP proxying and file system operations, minimizing latency and maximizing throughput, especially considering potential large file transfers or high-frequency network I/O.

---