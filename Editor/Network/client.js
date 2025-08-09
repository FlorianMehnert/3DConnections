const WebSocket = require('ws');
const ws = new WebSocket('ws://localhost:5678/dependencies');
ws.on('open', () => console.log('Connected to Unity Dependency Stream'));
ws.on('message', (data) => console.log("Received:", data.toString()));
