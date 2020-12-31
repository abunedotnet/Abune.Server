kubectl create namespace abune
kubectl config set-context --current --namespace=abune
helm install abune-server ./abune-server

