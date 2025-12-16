# MAURF-dVRK Website Setup Guide

This guide will help you set up and customize your GitHub Pages site.

## Quick Start

1. **Enable GitHub Pages**
   - Go to your repository settings
   - Navigate to "Pages" section
   - Under "Source", select "main" branch and "/ (root)" folder
   - Click Save
   - Your site will be available at: `https://luigimuratore.github.io/MAURF-dVRK/`

2. **Customize Content**
   - Edit `index.html` to update:
     - Title and subtitle
     - Author information and affiliations
     - Abstract text
     - Publications
     - Contact information
   - Update button links (Paper, Documentation)

3. **Add Media**
   - Place images in `assets/images/`
   - Add videos or YouTube embeds
   - See README files in each assets folder for details

## Customization Guide

### Update Title and Header
Edit lines 12-18 in `index.html`:
```html
<h1>MAURF-dVRK</h1>
<h2 class="subtitle">Your Framework Subtitle</h2>
```

### Update Author Information
Edit lines 20-26 in `index.html`:
```html
<div class="author-info">
    <span class="institution">Your Institution</span>
    <p class="affiliation">Research Lab / Department</p>
</div>
```

### Update Button Links
Edit lines 30-50 in `index.html` to add your actual links:
```html
<a href="YOUR_PAPER_URL" class="btn btn-primary">Paper</a>
<a href="YOUR_DOCS_URL" class="btn btn-primary">Documentation</a>
```

### Update Abstract
Edit lines 62-88 in `index.html` with your framework's description.

### Add Real Images
1. Copy your images to `assets/images/`
2. Update image paths in `index.html`:
   ```html
   <img src="assets/images/your-image.png" alt="Description">
   ```

### Add Videos

**For YouTube videos:**
1. Upload your video to YouTube
2. Get the video ID from the URL (e.g., `https://youtube.com/watch?v=VIDEO_ID`)
3. Uncomment and update the iframe code in `index.html`:
   ```html
   <iframe src="https://www.youtube.com/embed/VIDEO_ID"></iframe>
   ```

**For local videos:**
1. Place MP4 files in `assets/videos/`
2. Add video element:
   ```html
   <video controls>
       <source src="assets/videos/your-video.mp4" type="video/mp4">
   </video>
   ```

### Update Publications
Edit the publications section (around line 150 in `index.html`):
```html
<div class="publication">
    <h3>Your Paper Title</h3>
    <p class="authors-list">Author Names</p>
    <p class="venue"><em>Conference/Journal, Year</em></p>
    <div class="pub-links">
        <a href="PAPER_URL" class="link-btn">Paper</a>
        <a href="ARXIV_URL" class="link-btn">arXiv</a>
    </div>
</div>
```

### Update Citation
Edit the BibTeX entry (around line 170):
```bibtex
@article{yourname2024maurf,
    title={Your Paper Title},
    author={Your Name and Co-authors},
    journal={Journal Name},
    year={2024}
}
```

### Update Contact Information
Edit lines 180-190 in `index.html`:
```html
<li>Email: <a href="mailto:your.email@domain.com">your.email@domain.com</a></li>
```

## Color Customization

To change colors, edit `styles.css` variables (lines 7-15):
```css
:root {
    --primary-color: #2563eb;  /* Main accent color */
    --primary-hover: #1d4ed8;  /* Hover state */
    --text-color: #1f2937;     /* Main text */
    --text-secondary: #6b7280; /* Secondary text */
}
```

## Testing Locally

You can test your site locally before publishing:

### Option 1: Python Simple Server
```bash
cd /path/to/MAURF-dVRK
python3 -m http.server 8000
```
Visit: `http://localhost:8000`

### Option 2: VS Code Live Server
1. Install "Live Server" extension in VS Code
2. Right-click `index.html`
3. Select "Open with Live Server"

## Troubleshooting

### Images not showing
- Check file paths are correct (case-sensitive on Linux/GitHub)
- Verify images are in `assets/images/` directory
- Check file extensions match in HTML

### Videos not playing
- For YouTube: Check embed link format
- For local: Use MP4 format with H.264 codec
- Keep file sizes reasonable (<50MB)

### GitHub Pages not updating
- Changes may take 1-5 minutes to deploy
- Check repository settings > Pages is enabled
- Clear browser cache or use incognito mode

## File Structure
```
MAURF-dVRK/
├── index.html          # Main webpage
├── styles.css          # Styling
├── README.md           # Project README
├── SETUP.md           # This file
└── assets/
    ├── images/        # Image files
    │   └── README.md
    └── videos/        # Video files
        └── README.md
```

## Additional Resources

- [GitHub Pages Documentation](https://docs.github.com/en/pages)
- [HTML Tutorial](https://developer.mozilla.org/en-US/docs/Web/HTML)
- [CSS Tutorial](https://developer.mozilla.org/en-US/docs/Web/CSS)
- [Markdown Guide](https://www.markdownguide.org/)

## Need Help?

If you encounter issues:
1. Check the browser console (F12) for errors
2. Validate HTML at [W3C Validator](https://validator.w3.org/)
3. Review GitHub Pages build status in repository settings

---

Good luck with your project website! 🚀
