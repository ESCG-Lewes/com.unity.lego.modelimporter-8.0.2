// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited
// Place this file in: Runtime/BrickExtensions.cs (NOT in Editor folder)

using UnityEngine;

namespace LEGOModelImporter
{
    /// <summary>
    /// Extension methods for Brick class to support decoupling
    /// </summary>
    public static class BrickExtensions
    {
        /// <summary>
        /// Disconnects a brick from all connected bricks
        /// </summary>
        /// <param name="brick">The brick to disconnect</param>
        public static void DisconnectAll(this Brick brick)
        {
            if (brick == null) return;

            // Get all connected bricks
            var connectedBricks = brick.GetConnectedBricks(true);
            
            // Disconnect from each connected brick
            foreach (var connectedBrick in connectedBricks)
            {
                if (connectedBrick != null && connectedBrick != brick)
                {
                    // Disconnect both ways
                    DisconnectBricks(brick, connectedBrick);
                }
            }
        }

        /// <summary>
        /// Disconnects two bricks from each other
        /// </summary>
        private static void DisconnectBricks(Brick brick1, Brick brick2)
        {
            if (brick1 == null || brick2 == null) return;

            // Go through all parts in both bricks
            foreach (var part1 in brick1.parts)
            {
                if (part1 == null || part1.connectivity == null) continue;

                foreach (var field in part1.connectivity.planarFields)
                {
                    if (field == null) continue;

                    foreach (var connection in field.connections)
                    {
                        if (connection == null || connection.connectedTo == null) continue;

                        // Check if this connection connects to brick2
                        if (connection.connectedTo.field != null &&
                            connection.connectedTo.field.connectivity != null &&
                            connection.connectedTo.field.connectivity.part != null &&
                            connection.connectedTo.field.connectivity.part.brick == brick2)
                        {
                            // Disconnect
                            var connectedConnection = connection.connectedTo;
                            connection.connectedTo = null;
                            if (connectedConnection != null)
                            {
                                connectedConnection.connectedTo = null;
                            }
                        }
                    }
                }
            }

            // Also check brick2's connections to brick1 (in case we missed any)
            foreach (var part2 in brick2.parts)
            {
                if (part2 == null || part2.connectivity == null) continue;

                foreach (var field in part2.connectivity.planarFields)
                {
                    if (field == null) continue;

                    foreach (var connection in field.connections)
                    {
                        if (connection == null || connection.connectedTo == null) continue;

                        // Check if this connection connects to brick1
                        if (connection.connectedTo.field != null &&
                            connection.connectedTo.field.connectivity != null &&
                            connection.connectedTo.field.connectivity.part != null &&
                            connection.connectedTo.field.connectivity.part.brick == brick1)
                        {
                            // Disconnect
                            var connectedConnection = connection.connectedTo;
                            connection.connectedTo = null;
                            if (connectedConnection != null)
                            {
                                connectedConnection.connectedTo = null;
                            }
                        }
                    }
                }
            }
        }
    }
}