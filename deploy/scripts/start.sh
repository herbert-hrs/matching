#!/bin/bash
cd /home/ubuntu/$APPLICATION_NAME
docker-compose -f docker-compose-matching.yml up -d
