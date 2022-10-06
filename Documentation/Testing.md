# Getting started with testing

Most tests can be run without any special commands or processes. Running the following command from the project's root directory will restore packages, build the solution, and run the full CoreWCF test suite:

    dotnet test src/CoreWCF.sln

## Running integration tests

Some integration tests are dependent on external services (e.g. `CoreWCF.RabbitMQ.Tests` require access to a RabbitMQ host), thereby requiring additional setup before those tests can run successfully. The steps below will create local containers hosting all dependent services required by the integration tests.

1. [Install docker v19.03.0+](https://docs.docker.com/engine/install/) to your development environment via Docker Desktop or the docker engine
2. From the root directory of the CoreWCF repo, run the following command to start the service containers:

        docker-compose up -d
		
3. Run the tests as usual, using your IDE or running the following command from the command line:

        dotnet test src/CoreWCF.sln

4. After running the tests, run the following command to stop the service containers and clean up all related artifacts (images, networks, volumes, etc):

        docker-compose down --rmi -v

    Alternatively, the following command will stop the service containers while preserving container artifacts:

        docker-compose stop

## Platform specific tests

Note: Some unit tests are set to only run on specific platforms (i.e. Windows-only or Linux-only) based on the features available to those operating systems. 
