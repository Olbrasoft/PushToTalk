// PushToTalk Remote Control
// Connects to SignalR hub and provides remote recording control

const elements = {
    connectionStatus: document.getElementById('connectionStatus'),
    btnToggle: document.getElementById('btnToggle'),
    btnEnter: document.getElementById('btnEnter'),
    btnClear: document.getElementById('btnClear'),
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
    elements.btnEnter.disabled = !connected;
    elements.btnClear.disabled = !connected;
}

function setRecordingState(recording, transcribing) {
    isRecording = recording;
    isTranscribing = transcribing;

    console.log('setRecordingState called:', { recording, transcribing });

    // Update toggle button - 3 states: Idle (blue), Recording (red), Transcribing (yellow)
    if (recording) {
        // State: Recording - RED button with Stop + timer
        elements.btnToggle.classList.remove('transcribing');
        elements.btnToggle.classList.add('recording');
        elements.toggleIcon.textContent = '■'; // Stop square
        elements.toggleText.textContent = 'Stop';
        recordingStartTime = Date.now();
        startDurationTimer();
        console.log('Button should be red now (Recording), timer started');
    } else if (transcribing) {
        // State: Transcribing - YELLOW button with Cancel (X)
        elements.btnToggle.classList.remove('recording');
        elements.btnToggle.classList.add('transcribing');
        elements.toggleIcon.textContent = '✕'; // X symbol
        elements.toggleText.textContent = 'Zrusit';
        stopDurationTimer();
        console.log('Button should be yellow now (Transcribing)');
    } else {
        // State: Idle - BLUE button with Record
        elements.btnToggle.classList.remove('recording', 'transcribing');
        elements.toggleIcon.textContent = '●'; // Bullet/circle for record
        elements.toggleText.textContent = 'Diktovat';
        stopDurationTimer();
        console.log('Button should be blue now (Idle)');
    }

    // Enter and Clear buttons are always enabled when connected (handled by setConnectionStatus)
}

function setTranscriptionText(text) {
    elements.transcriptionText.textContent = text;
    elements.transcriptionText.classList.remove('empty');
}

function startDurationTimer() {
    stopDurationTimer();
    durationInterval = setInterval(() => {
        if (recordingStartTime && isRecording) {
            const elapsed = Math.floor((Date.now() - recordingStartTime) / 1000);
            const minutes = Math.floor(elapsed / 60).toString().padStart(2, '0');
            const seconds = (elapsed % 60).toString().padStart(2, '0');
            elements.toggleText.textContent = `Nahravani ${minutes}:${seconds}`;
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

// Haptic feedback handler (separate from click to ensure it works on Xiaomi devices)
// Using pointerdown because click event is not always trusted on MIUI devices
elements.btnToggle.addEventListener('pointerdown', () => {
    // Don't vibrate if button is disabled (prevents confusion during processing)
    if (elements.btnToggle.disabled) {
        console.log('Button disabled, skipping vibration');
        return;
    }

    if ('vibrate' in navigator) {
        let pattern;
        if (isTranscribing) {
            // Canceling transcription - short vibration
            pattern = 30;
        } else if (isRecording) {
            // Stopping recording - medium vibration
            pattern = 50;
        } else {
            // Starting recording - double vibration pattern
            pattern = [100, 50, 100];
        }
        console.log('Vibration API available, attempting vibration pattern:', pattern);
        const result = navigator.vibrate(pattern);
        console.log('Vibration result:', result, 'isTrusted:', event.isTrusted);
    } else {
        console.warn('Vibration API not supported on this device');
    }
});

// Button handlers
elements.btnToggle.addEventListener('click', async () => {
    try {
        console.log('Toggle button clicked, isRecording:', isRecording, 'isTranscribing:', isTranscribing);
        elements.btnToggle.disabled = true;

        if (isTranscribing) {
            // Cancel transcription (yellow button state)
            console.log('Sending CancelTranscription');
            await connection.invoke('CancelTranscription');
        } else {
            // Toggle recording (blue <-> red)
            console.log('Sending ToggleRecording');
            await connection.invoke('ToggleRecording');
        }
    } catch (error) {
        console.error('Toggle failed:', error);
    } finally {
        elements.btnToggle.disabled = false;
    }
});

elements.btnEnter.addEventListener('click', async () => {
    try {
        elements.btnEnter.disabled = true;
        await connection.invoke('PressEnter');
    } catch (error) {
        console.error('Enter failed:', error);
    } finally {
        elements.btnEnter.disabled = false;
    }
});

elements.btnClear.addEventListener('click', async () => {
    try {
        elements.btnClear.disabled = true;
        await connection.invoke('ClearText');
    } catch (error) {
        console.error('Clear failed:', error);
    } finally {
        elements.btnClear.disabled = false;
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
