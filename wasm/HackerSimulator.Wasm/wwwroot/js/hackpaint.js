export function init(id){
    const canvas = document.getElementById(id);
    canvas._hp_ctx = canvas.getContext('2d');
}

export function startDraw(id, x, y, color, size){
    const ctx = document.getElementById(id)._hp_ctx;
    ctx.beginPath();
    ctx.strokeStyle = color;
    ctx.lineWidth = size;
    ctx.lineCap = 'round';
    ctx.moveTo(x,y);
}

export function draw(id, x, y){
    const ctx = document.getElementById(id)._hp_ctx;
    ctx.lineTo(x,y);
    ctx.stroke();
}

export function endDraw(id){
    const ctx = document.getElementById(id)._hp_ctx;
    ctx.closePath();
}

export function clear(id, width, height, color){
    const canvas = document.getElementById(id);
    const ctx = canvas._hp_ctx;
    canvas.width = width;
    canvas.height = height;
    if(color){
        ctx.fillStyle = color;
        ctx.fillRect(0,0,width,height);
    }else{
        ctx.clearRect(0,0,width,height);
    }
}

export function loadImage(id, dataUrl){
    return new Promise((resolve, reject)=>{
        const img = new Image();
        img.onload = () => {
            const canvas = document.getElementById(id);
            canvas.width = img.width;
            canvas.height = img.height;
            const ctx = canvas._hp_ctx;
            ctx.drawImage(img,0,0);
            resolve();
        };
        img.onerror = reject;
        img.src = dataUrl;
    });
}

export function rotate90(id){
    const canvas = document.getElementById(id);
    const temp = document.createElement('canvas');
    temp.width = canvas.width;
    temp.height = canvas.height;
    const tctx = temp.getContext('2d');
    tctx.drawImage(canvas,0,0);
    canvas.width = temp.height;
    canvas.height = temp.width;
    const ctx = canvas._hp_ctx;
    ctx.save();
    ctx.translate(canvas.width,0);
    ctx.rotate(Math.PI/2);
    ctx.drawImage(temp,0,0);
    ctx.restore();
}

export function toDataUrl(id, mime, quality){
    return document.getElementById(id).toDataURL(mime, quality);
}

export function setScale(id, scale){
    document.getElementById(id).style.transform = `scale(${scale})`;
}

export function toggleGrid(id){
    document.getElementById(id).classList.toggle('grid');
}

export function download(dataUrl, name){
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = name;
    a.click();
}
