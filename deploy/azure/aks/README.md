# Setup Azure Kubernetes Cluster (AKS)

## Prerequisites

- A) Create or use an existing **Azure Account** (https://azure.microsoft.com/en-us/free/)
- B) Create or use an existing AKS/kubernetes **cluster** (https://docs.microsoft.com/en-us/azure/aks/kubernetes-walkthrough-portal)
- C) Install the kubernetes managing tool "**kubectl**" (Windows: https://docs.microsoft.com/en-us/powershell/module/az.aks/install-azakskubectl, Linux: https://kubernetes.io/de/docs/tasks/tools/install-kubectl/)
- D) Install the kubernetes package manager tool "**helm**" (https://helm.sh/docs/intro/install/)

## Networking
To setup an kubernetes cluster on azure (aks) you need to setup some network properties manually.
It is caused by the lack of full UDP support using the integrated k8s load balancer feature.
To work around this issue we publish our endpoint using NodePort and setting up the Azure Load Balancer manually.
Further investigation is needed to verify what exactly is causing issues aks UDP load balancer.

#### Networking - Basic load balancer configuration (supporting scale out)
Using a cluster the configuration with load balancing looks similar to this:

```
[Client:Any] => [AKS LoadBalancer:7777] => [NodePort-1:30777] => [abune-server-1:7777]
                                        => [NodePort-2:30777] => [abune-server-2:7777]
					=> [NodePort-3:30777] => [abune-server-3:7777]
					...
					=> [NodePort-n:30777] => [abune-server-n:7777]
```									

- **Port 7777/30777 (UDP)** is used for the core game communication.
- **Port 7778/30778 (TCP)** is used for internal akka cluster communication and also used for health probing, so that the load balancer could verify the node is up and running.

#### Networking - Health probes
Add a new health probe so the load balancer knows the service is up and running.
Because health probes do not support UDP we are using the akka cluster communication port.

- **Location**: [ResourceGroup] > Load Balancer (Kubernetes) > Settings > Health probe
- **Name**: AkkaCluster 
- **Protocol**: TCP
- **Port**: 30778
- **Interval**: 5
- **Unhealthy threshold**: 2

#### Networking - Load Balancer
Add a new load balancing rule. 

- **Location**: [ResourceGroup] > Load Balancer (Kubernetes) > Settings > Load Balancing Rules
- **Name**: GameServer 
- **IP Version**: IPv4 (or IPv6)
- **Frontend IP address**: f.e. choose default <51.xxx.xxx.xxx>
- **Protocol**: UDP
- **Port**: 7777
- **Backend Port**: 30777
- **Backend Pool**: f.e. choose default <aksOutboundBackendPool>
- **Health Probe**: AkkaCluster (TCP:30778)
- **Session Persistence**: Client IP
- **Floating IP**: enabled
- **SNAT**: Use outbound rules ... (recommended)

#### Networking - Inbound firewall rules
Add firewall rules for inbound udp traffic.

- **Location**: [ResourceGroup] > Network Security Group (aks....-nsg) > Settings > Inbound security rules
- **Source**: Any
- **Source Port Ranges**: *
- **Destination**: Any
- **Destination Port Ranges**: 30777
- **Protocol**: UDP
- **Action**: Allow
- **Priority**: 100
- **Name**: GameServerRelay

### Installation / Deployment
To deploy to your AKS cluster you need to login. In this example we are using a specific k8s namespace to deploy the installation.

```
PS > az login
PS > kubectl create namespace abune
PS > kubectl config set-context --current --namespace=abune
PS > helm install abune-server ./deploy/helm/abune-server
PS > kubectl get all
```

You can use ´--app-version <VERSION>´ if you don't want to deploy the docker-version you checked out.

The output should so something like this:

```
NAME                                READY   STATUS    RESTARTS   AGE
pod/abune-server-7cd97ffb65-vjrn7   1/1     Running   0          11s

NAME                   TYPE       CLUSTER-IP     EXTERNAL-IP   PORT(S)                         AGE
service/abune-server   NodePort   10.0.181.179   <none>        7777:30777/UDP,7778:30778/TCP   11s

NAME                           READY   UP-TO-DATE   AVAILABLE   AGE
deployment.apps/abune-server   1/1     1            1           11s
```

### READY TO GO!
Connect your game client!

### Debugging network issues
In some cases it could be necessary to analyse network issues. 
Here are some random examples how to analyse network related issues.

#### Install network tools within pods
Connect to a pod 
```
kubectl exec --stdin --tty abune-server-7cd97ffb65-vjrn7 -- /bin/bash
```
and install network tools.
```
apt-get update
apt-get install -y net-tools
apt-get install -y iputils-ping
apt-get install -y iproute2
apt-get install -y dnsutils
```

#### Check network information within pods
Use netstat to see all listener ports and active connections.
```
> netstat -an
Active Internet connections (servers and established)
Proto Recv-Q Send-Q Local Address           Foreign Address         State
tcp6       0      0 0.0.0.0:7778            :::*                    LISTEN
tcp6       0      0 10.240.0.17:7778        10.240.0.4:63657        ESTABLISHED
udp6       0      0 0.0.0.0:7777            :::*
```

Use netstat to get informations about active tcp connections (Health Probe or inter cluster communication)
```
> netstat -p tcp
Proto Recv-Q Send-Q Local Address           Foreign Address         State       PID/Program name
tcp6       0      0 abune-server-7cd97:7778 aks-agentpool-357:63657 ESTABLISHED 1/dotnet
```

You can check how many udp packets where sent.
```
> netstat -e -s
Udp:
    1023121246 packets received
    0 packets to unknown port received
    0 packet receive errors
    2312154421 packets sent
    0 receive buffer errors
    0 send buffer errors
```

You can check the kube-proxy configuration.
```
> netstat -e -s
Udp:
    1023121246 packets received
    0 packets to unknown port received
    0 packet receive errors
    2312154421 packets sent
    0 receive buffer errors
    0 send buffer errors
```

### Check network information from outside 

You can check list all network namespaces. 
```
> kubectl get service --all-namespaces
```

