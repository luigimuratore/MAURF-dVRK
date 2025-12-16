## Video Options:

### Option 1: YouTube Videos (Recommended)
Upload your videos to YouTube and embed them using iframe:

```html
<div class="video-container">
    <div class="video-wrapper">
        <iframe 
            src="https://www.youtube.com/embed/YOUR_VIDEO_ID" 
            title="YouTube video player" 
            allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" 
            allowfullscreen>
        </iframe>
    </div>
    <p class="caption">Your video caption here</p>
</div>
```

### Option 2: Local Video Files
If hosting videos locally:

1. **Formats**: MP4 (H.264) is recommended for best browser compatibility
2. **File size**: Keep under 50MB. Consider compressing or using YouTube for larger files
3. **Resolution**: 1920x1080 (1080p) or 1280x720 (720p)

Example HTML for local video:
```html
<div class="video-container">
    <video class="local-video" controls>
        <source src="assets/videos/your-video.mp4" type="video/mp4">
        Your browser does not support the video tag.
    </video>
    <p class="caption">Your video caption here</p>
</div>
```

Add this CSS to styles.css for local videos:
```css
.local-video {
    width: 100%;
    border-radius: 0.5rem;
    box-shadow: var(--shadow-md);
}
```
