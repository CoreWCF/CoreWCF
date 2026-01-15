#!/bin/env bash

set -eu

PASS=changeme

echo "0. Clean kafka-secrets directory"
rm -rf ca-root.*
rm -rf broker.*
rm -rf consumer.*
rm -rf producer.*

# see https://github.com/confluentinc/confluent-kafka-dotnet/tree/master/examples/TlsAuth

echo "1. Create a private key / public key certificate pair for the broker"
keytool -keystore broker.keystore.jks -alias broker -validity 365 -genkey -keyalg RSA -dname "cn=localhost" -storepass $PASS -keypass $PASS
echo "2. Create a Certificate Authority (CA) private key / root certificate"
openssl req -nodes -new -x509 -keyout ca-root.key -out ca-root.crt -days 365 -subj "/C=US/ST=WA/L=SE/O=COREWCF/CN=COREWCF"
echo "3. Sign the broker public key certificate"
echo "  i. Generate a Certificate Signing Request (CSR) from the self-signed certificate you created in step 1 housed inside the server keystore file"
keytool -keystore broker.keystore.jks -alias broker -certreq -file broker.csr -storepass $PASS
echo "  ii. Use the CA key pair you generated in step 2 to create a CA signed certificate from the CSR you just created"
openssl x509 -req -CA ca-root.crt -CAkey ca-root.key -in broker.csr -out broker.crt -days 365 -CAcreateserial
echo "  iii. Import this signed certificate into your server keystore (over-writing the existing self-signed one). Before you can do this, you'll need to add the CA public key certificate as well"
keytool -keystore broker.keystore.jks -alias CARoot -import -noprompt -file ca-root.crt -storepass $PASS
keytool -keystore broker.keystore.jks -alias broker -import -file broker.crt -storepass $PASS
echo "4. Create the broker keystore credential"
echo $PASS > broker.keystore.jks.cred
echo "5. Create the broker truststore"
keytool -keystore broker.truststore.jks -alias CARoot -import -file ca-root.crt -storepass $PASS -keypass $PASS -noprompt
echo "6. Create the broker truststore credential"
echo $PASS > broker.truststore.jks.cred

for client in producer consumer
do
    echo "1. Create a private key / public key certificate pair for $client"
    keytool -keystore $client.keystore.jks -alias $client -validity 365 -genkey -keyalg RSA -dname "cn=$client" -storepass $PASS -keypass $PASS
    echo "2. Create a CA signed certificate"
    keytool -keystore $client.keystore.jks -alias $client -certreq -file $client.csr -storepass $PASS
    openssl x509 -req -CA ca-root.crt -CAkey ca-root.key -in $client.csr -out $client.crt -days 365 -CAcreateserial
    echo "3. Package client key and certificate into a single PKCS12 file"
    keytool -importkeystore -srckeystore $client.keystore.jks -destkeystore $client.keystore.p12 -srcstoretype JKS -deststoretype PKCS12 -srcstorepass $PASS -deststorepass $PASS
    echo "4. Create the client keystore credential"
    echo $PASS > $client.keystore.p12.cred
    echo "5. Export the client key"
    openssl pkcs12 -in $client.keystore.p12 -nocerts -nodes -passin pass:$PASS | openssl pkey -out $client.key -passout pass:$PASS
    echo "6. Create the client key credential"
    echo $PASS > $client.key.cred
done

echo "1. Verify broker certificate"
openssl verify -CAfile ca-root.crt broker.crt
echo "2. Verify consumer certificate"
openssl verify -CAfile ca-root.crt consumer.crt
echo "3. Verify producer certificate"
openssl verify -CAfile ca-root.crt producer.crt
echo "4. Apply file permissions"
chmod 644 *.key
chmod 644 *.p12

exit 0
