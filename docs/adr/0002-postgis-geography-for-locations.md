# Store locations as PostGIS geography points

Asset locations are `geography(Point, 4326)` and Area boundaries are geography polygons, so the database can answer "which Area contains this point" and "which Assets are in this map view". We rejected two plain latitude and longitude columns because they cannot do these queries well. Local and production Postgres both need the PostGIS extension.
