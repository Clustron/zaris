# Getting Started with Clustron DKV (Windows)

Follow the steps below to install and run Clustron DKV on a Windows machine.

---

## 1. Download the Release

1. Download `clustron-dkv-0.1.0-win-x64.zip` from the **Releases** page.
2. Extract the ZIP file to a folder of your choice.

---

## 2. Install PowerShell 7

Clustron DKV requires **PowerShell 7.5.4 or later**.

If you don’t already have it installed, download it from:

https://learn.microsoft.com/powershell/

---

## 3. Run PowerShell as Administrator

Open **PowerShell 7** with administrative privileges.

---

## 4. Install Clustron DKV

Navigate to the extracted folder:

```
/clustron/
```

Run the installation script:

```powershell
./install.ps1
```

This installs Clustron DKV to:

```
C:\Program Files\Clustron
```

---

## Example Setup

Assume your machine’s IP address is:

```
10.0.0.4
```

All example commands below use this IP.  
Replace it with your actual IP address if different.

---

## 5. Connect to the Management Service

```powershell
Connect-DkvManager 10.0.0.4:7801
```

If running locally, you may also use:

```powershell
Connect-DkvManager localhost:7801
```

---

## 6. Create a New Store

The following command creates a store named **TestStore** with two nodes running on the same machine (for demo purposes).

```powershell
New-DkvStore `
  -Name TestStore `
  -Instances @(
      @{ InstanceName="node1"; ClustronPort=7805; ClientPort=7070 },
      @{ InstanceName="node2"; ClustronPort=7806; ClientPort=7071 }
  ) `
  -Server 10.0.0.4
```

This creates:

- node1 → ClustronPort 7805, ClientPort 7070  
- node2 → ClustronPort 7806, ClientPort 7071  

---

## 7. Start the Store

```powershell
Start-DkvStore TestStore
```

This starts all instances of the store.

---

## 8. Monitor Store Statistics

Open live per-second metrics for all instances:

```powershell
Watch-DkvStoreMetrics -StoreName TestStore
```

You’ll see live counters for operations per second.

---

## 9. Open a Second PowerShell Terminal

Keep the first terminal running metrics.

Open a new PowerShell 7 window.

---

## 10. Connect to the Store

Connect to any one node. The client will automatically discover the rest of the cluster.

```powershell
Connect-DkvStore `
  -StoreName TestStore `
  -Endpoints 10.0.0.4:7070
```

(You can use any valid client port.)

---

## 11. Generate Load (Stress Test)

Simulate concurrent load and watch metrics update in real time:

```powershell
Stress-DkvStore `
  -StoreName TestStore `
  -Concurrency 32 `
  -DurationSec 500
```

This runs a stress test with:

- 32 concurrent operations  
- 500 seconds duration  

---

## You’re Done!

You now have:

- Installed Clustron DKV  
- Created a multi-node store  
- Started the cluster  
- Connected a client  
- Generated load  
- Monitored real-time metrics  
