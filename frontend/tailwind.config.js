const primeui = require('tailwindcss-primeui');

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  darkMode: ['selector', '[class="app-dark"]'],
  theme: {
    extend: {}
  },
  plugins: [primeui]
};
