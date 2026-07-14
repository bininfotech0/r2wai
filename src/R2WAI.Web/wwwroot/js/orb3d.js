// R2WAI Voice Orb — dependency-free WebGL 3D avatar (no vendored third-party library).
// Renders a faceted low-poly sphere (flat-shaded icosahedron projected onto a unit sphere)
// that rotates and pulses based on voice state: idle | listening | thinking | speaking.

const STATES = {
    idle: { color: [0.486, 0.227, 0.929], rotSpeed: 0.15, pulseSpeed: 1.2, pulseAmount: 0.30 },
    listening: { color: [0.298, 0.686, 0.314], rotSpeed: 0.45, pulseSpeed: 3.0, pulseAmount: 0.80 },
    thinking: { color: [0.961, 0.620, 0.043], rotSpeed: 1.4, pulseSpeed: 0.6, pulseAmount: 0.15 },
    speaking: { color: [0.310, 0.275, 0.898], rotSpeed: 0.6, pulseSpeed: 5.0, pulseAmount: 1.0 },
};

const VERTEX_SHADER = `
    attribute vec3 aPosition;
    attribute vec3 aNormal;
    uniform mat4 uMvp;
    uniform mat4 uRotation;
    varying vec3 vNormal;
    void main() {
        vNormal = normalize((uRotation * vec4(aNormal, 0.0)).xyz);
        gl_Position = uMvp * vec4(aPosition, 1.0);
    }
`;

const FRAGMENT_SHADER = `
    precision mediump float;
    varying vec3 vNormal;
    uniform vec3 uColor;
    uniform float uPulse;
    void main() {
        vec3 lightDir = normalize(vec3(0.4, 0.6, 1.0));
        vec3 viewDir = vec3(0.0, 0.0, 1.0);
        float diff = max(dot(vNormal, lightDir), 0.0);
        float rim = pow(1.0 - max(dot(vNormal, viewDir), 0.0), 2.5);
        vec3 color = uColor * (0.4 + 0.6 * diff) + uColor * rim * (1.0 + uPulse);
        gl_FragColor = vec4(color, 1.0);
    }
`;

function buildIcosahedronFacets() {
    const t = (1 + Math.sqrt(5)) / 2;
    const raw = [
        [-1, t, 0], [1, t, 0], [-1, -t, 0], [1, -t, 0],
        [0, -1, t], [0, 1, t], [0, -1, -t], [0, 1, -t],
        [t, 0, -1], [t, 0, 1], [-t, 0, -1], [-t, 0, 1],
    ].map(v => {
        const len = Math.hypot(v[0], v[1], v[2]);
        return [v[0] / len, v[1] / len, v[2] / len];
    });

    const faces = [
        [0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11],
        [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8],
        [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9],
        [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1],
    ];

    const positions = [];
    const normals = [];

    for (const [ia, ib, ic] of faces) {
        const a = raw[ia], b = raw[ib], c = raw[ic];
        const ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2];
        const vx = c[0] - a[0], vy = c[1] - a[1], vz = c[2] - a[2];
        let nx = uy * vz - uz * vy;
        let ny = uz * vx - ux * vz;
        let nz = ux * vy - uy * vx;
        const nlen = Math.hypot(nx, ny, nz) || 1;
        nx /= nlen; ny /= nlen; nz /= nlen;

        for (const v of [a, b, c]) {
            positions.push(v[0], v[1], v[2]);
            normals.push(nx, ny, nz);
        }
    }

    return { positions: new Float32Array(positions), normals: new Float32Array(normals), count: faces.length * 3 };
}

function mat4Identity() {
    return new Float32Array([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
}

function mat4Multiply(a, b) {
    const out = new Float32Array(16);
    for (let i = 0; i < 4; i++) {
        for (let j = 0; j < 4; j++) {
            let sum = 0;
            for (let k = 0; k < 4; k++) sum += a[k * 4 + j] * b[i * 4 + k];
            out[i * 4 + j] = sum;
        }
    }
    return out;
}

function mat4Perspective(fovyRad, aspect, near, far) {
    const f = 1.0 / Math.tan(fovyRad / 2);
    const out = new Float32Array(16);
    out[0] = f / aspect;
    out[5] = f;
    out[10] = (far + near) / (near - far);
    out[11] = -1;
    out[14] = (2 * far * near) / (near - far);
    return out;
}

function mat4Translate(x, y, z) {
    const out = mat4Identity();
    out[12] = x; out[13] = y; out[14] = z;
    return out;
}

function mat4RotateY(a) {
    const c = Math.cos(a), s = Math.sin(a);
    const out = mat4Identity();
    out[0] = c; out[2] = -s; out[8] = s; out[10] = c;
    return out;
}

function mat4RotateX(a) {
    const c = Math.cos(a), s = Math.sin(a);
    const out = mat4Identity();
    out[5] = c; out[6] = s; out[9] = -s; out[10] = c;
    return out;
}

function compileShader(gl, type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        gl.deleteShader(shader);
        return null;
    }
    return shader;
}

let _instances = new Map();

export function init(canvas) {
    if (!canvas) return false;
    const gl = canvas.getContext('webgl', { alpha: true, antialias: true, premultipliedAlpha: false })
        || canvas.getContext('experimental-webgl', { alpha: true });
    if (!gl) return false;

    const vs = compileShader(gl, gl.VERTEX_SHADER, VERTEX_SHADER);
    const fs = compileShader(gl, gl.FRAGMENT_SHADER, FRAGMENT_SHADER);
    if (!vs || !fs) return false;

    const program = gl.createProgram();
    gl.attachShader(program, vs);
    gl.attachShader(program, fs);
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) return false;

    const geo = buildIcosahedronFacets();

    const posBuffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, posBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, geo.positions, gl.STATIC_DRAW);

    const normBuffer = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, normBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, geo.normals, gl.STATIC_DRAW);

    const aPosition = gl.getAttribLocation(program, 'aPosition');
    const aNormal = gl.getAttribLocation(program, 'aNormal');
    const uMvp = gl.getUniformLocation(program, 'uMvp');
    const uRotation = gl.getUniformLocation(program, 'uRotation');
    const uColor = gl.getUniformLocation(program, 'uColor');
    const uPulse = gl.getUniformLocation(program, 'uPulse');

    const instance = {
        gl, canvas, program, posBuffer, normBuffer, aPosition, aNormal, uMvp, uRotation, uColor, uPulse,
        vertexCount: geo.count,
        state: STATES.idle,
        stateName: 'idle',
        startTime: performance.now(),
        rafId: 0,
        disposed: false,
    };

    _instances.set(canvas, instance);
    gl.enable(gl.DEPTH_TEST);
    gl.clearColor(0, 0, 0, 0);

    renderLoop(instance);
    return true;
}

function renderLoop(instance) {
    if (instance.disposed) return;

    const { gl, canvas } = instance;
    const displayWidth = canvas.clientWidth || 96;
    const displayHeight = canvas.clientHeight || 96;
    if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
        canvas.width = displayWidth;
        canvas.height = displayHeight;
    }
    gl.viewport(0, 0, canvas.width, canvas.height);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

    const elapsed = (performance.now() - instance.startTime) / 1000;
    const s = instance.state;
    const pulse = Math.sin(elapsed * s.pulseSpeed) * 0.5 + 0.5;
    const scale = 1.0 + pulse * s.pulseAmount * 0.15;

    const rotation = mat4Multiply(mat4RotateY(elapsed * s.rotSpeed), mat4RotateX(elapsed * s.rotSpeed * 0.6));
    const scaleMat = mat4Identity();
    scaleMat[0] = scale; scaleMat[5] = scale; scaleMat[10] = scale;
    const model = mat4Multiply(rotation, scaleMat);

    const view = mat4Translate(0, 0, -2.6);
    const aspect = canvas.width / canvas.height || 1;
    const projection = mat4Perspective(Math.PI / 4, aspect, 0.1, 100);
    const mvp = mat4Multiply(projection, mat4Multiply(view, model));

    gl.useProgram(instance.program);

    gl.bindBuffer(gl.ARRAY_BUFFER, instance.posBuffer);
    gl.enableVertexAttribArray(instance.aPosition);
    gl.vertexAttribPointer(instance.aPosition, 3, gl.FLOAT, false, 0, 0);

    gl.bindBuffer(gl.ARRAY_BUFFER, instance.normBuffer);
    gl.enableVertexAttribArray(instance.aNormal);
    gl.vertexAttribPointer(instance.aNormal, 3, gl.FLOAT, false, 0, 0);

    gl.uniformMatrix4fv(instance.uMvp, false, mvp);
    gl.uniformMatrix4fv(instance.uRotation, false, rotation);
    gl.uniform3fv(instance.uColor, s.color);
    gl.uniform1f(instance.uPulse, pulse * s.pulseAmount);

    gl.drawArrays(gl.TRIANGLES, 0, instance.vertexCount);

    instance.rafId = requestAnimationFrame(() => renderLoop(instance));
}

export function setState(canvas, state) {
    const instance = _instances.get(canvas);
    if (!instance) return;
    if (!STATES[state]) return;
    instance.stateName = state;
    instance.state = STATES[state];
}

export function dispose(canvas) {
    const instance = _instances.get(canvas);
    if (!instance) return;
    instance.disposed = true;
    if (instance.rafId) cancelAnimationFrame(instance.rafId);
    _instances.delete(canvas);
}
