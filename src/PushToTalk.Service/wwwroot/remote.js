// PushToTalk Remote Control
// Connects to SignalR hub and provides remote recording control

const elements = {
    connectionStatus: document.getElementById('connectionStatus'),
    statusDot: document.getElementById('statusDot'),
    statusText: document.getElementById('statusText'),
    duration: document.getElementById('duration'),
    btnToggle: document.getElementById('btnToggle'),
    btnStart: document.getElementById('btnStart'),
    btnStop: document.getElementById('btnStop'),
    toggleIcon: document.getElementById('toggleIcon'),
    toggleText: document.getElementById('toggleText'),
    transcriptionText: document.getElementById('transcriptionText')
};

let connection = null;
let isRecording = false;
let isTranscribing = false;
let durationInterval = null;
let recordingStartTime = null;

// Build SignalR connection
function buildConnection() {
    // Use relative URL - works on any host
    const hubUrl = window.location.origin + '/hubs/ptt';

    connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Connection lifecycle events
    connection.onreconnecting((error) => {
        console.log('Reconnecting...', error);
        setConnectionStatus(false);
    });

    connection.onreconnected((connectionId) => {
        console.log('Reconnected:', connectionId);
        setConnectionStatus(true);
        refreshStatus();
    });

    connection.onclose((error) => {
        console.log('Connection closed:', error);
        setConnectionStatus(false);
    });

    // PTT Events from server
    connection.on('PttEvent', handlePttEvent);
    connection.on('Connected', (connectionId) => {
        console.log('Connected with ID:', connectionId);
    });
}

function handlePttEvent(event) {
    console.log('PttEvent:', event);

    switch (event.eventType) {
        case 0: // RecordingStarted
            setRecordingState(true, false);
            break;

        case 1: // RecordingStopped
            setRecordingState(false, false);
            break;

        case 2: // TranscriptionStarted
            setRecordingState(false, true);
            break;

        case 3: // TranscriptionCompleted
            setRecordingState(false, false);
            if (event.text) {
                setTranscriptionText(event.text);
            }
            break;

        case 4: // TranscriptionFailed
            setRecordingState(false, false);
            break;
    }
}

function setConnectionStatus(connected) {
    elements.connectionStatus.textContent = connected ? 'Pripojeno' : 'Odpojeno';
    elements.connectionStatus.className = 'connection-status ' + (connected ? 'connected' : 'disconnected');

    elements.btnToggle.disabled = !connected;
    elements.btnStart.disabled = !connected;
    elements.btnStop.disabled = !connected;

    if (!connected) {
        elements.statusDot.className = 'status-dot';
        elements.statusText.textContent = 'Odpojeno';
    }
}

function setRecordingState(recording, transcribing) {
    isRecording = recording;
    isTranscribing = transcribing;

    // Update status dot
    elements.statusDot.className = 'status-dot connected';
    if (recording) {
        elements.statusDot.classList.add('recording');
        elements.statusText.textContent = 'Nahrava se...';
    } else if (transcribing) {
        elements.statusDot.classList.add('transcribing');
        elements.statusText.textContent = 'Prepisuje se...';
    } else {
        elements.statusText.textContent = 'Pripraveno';
    }

    // Update toggle button
    if (recording) {
        elements.btnToggle.classList.add('recording');
        elements.toggleIcon.innerHTML = '&#9632;'; // Stop icon
        elements.toggleText.textContent = 'Zastavit';
    } else {
        elements.btnToggle.classList.remove('recording');
        elements.toggleIcon.innerHTML = '&#9658;'; // Play icon
        elements.toggleText.textContent = 'Zacit';
    }

    // Update duration display
    if (recording) {
        recordingStartTime = Date.now();
        elements.duration.classList.add('visible');
        startDurationTimer();
    } else {
        stopDurationTimer();
        if (!transcribing) {
            elements.duration.classList.remove('visible');
        }
    }

    // Update button states
    elements.btnStart.disabled = recording || !connection;
    elements.btnStop.disabled = !recording || !connection;
}

function setTranscriptionText(text) {
    elements.transcriptionText.textContent = text;
    elements.transcriptionText.classList.remove('empty');
}

function startDurationTimer() {
    stopDurationTimer();
    durationInterval = setInterval(() => {
        if (recordingStartTime) {
            const elapsed = Math.floor((Date.now() - recordingStartTime) / 1000);
            const minutes = Math.floor(elapsed / 60).toString().padStart(2, '0');
            const seconds = (elapsed % 60).toString().padStart(2, '0');
            elements.duration.textContent = `${minutes}:${seconds}`;
        }
    }, 100);
}

function stopDurationTimer() {
    if (durationInterval) {
        clearInterval(durationInterval);
        durationInterval = null;
    }
}

async function refreshStatus() {
    try {
        const status = await connection.invoke('GetStatus');
        console.log('Status:', status);
        setRecordingState(status.isRecording, status.isTranscribing);
    } catch (error) {
        console.error('Failed to get status:', error);
    }
}

// Button handlers
elements.btnToggle.addEventListener('click', async () => {
    try {
        elements.btnToggle.disabled = true;
        await connection.invoke('ToggleRecording');
    } catch (error) {
        console.error('Toggle failed:', error);
    } finally {
        elements.btnToggle.disabled = false;
    }
});

elements.btnStart.addEventListener('click', async () => {
    try {
        elements.btnStart.disabled = true;
        await connection.invoke('StartRecording');
    } catch (error) {
        console.error('Start failed:', error);
    } finally {
        elements.btnStart.disabled = isRecording;
    }
});

elements.btnStop.addEventListener('click', async () => {
    try {
        elements.btnStop.disabled = true;
        await connection.invoke('StopRecording');
    } catch (error) {
        console.error('Stop failed:', error);
    } finally {
        elements.btnStop.disabled = !isRecording;
    }
});

// Initialize
async function initialize() {
    buildConnection();

    try {
        await connection.start();
        console.log('SignalR connected');
        setConnectionStatus(true);
        await refreshStatus();
    } catch (error) {
        console.error('Failed to connect:', error);
        setConnectionStatus(false);

        // Retry after 5 seconds
        setTimeout(initialize, 5000);
    }
}

// Start when page loads
initialize();
