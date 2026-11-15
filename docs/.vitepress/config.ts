import { defineConfig } from 'vitepress'
import { withLikeC4 } from '@leberkas-org/vitepress-likec4'

export default withLikeC4({ likec4: { source: './likec4', height: '460px' } }, defineConfig({
  base: '/',
  title: 'njord',
  description: 'Multi-domain weather intelligence for Home Assistant',
  head: [['link', { rel: 'icon', type: 'image/svg+xml', href: '/logo.svg' }]],

  themeConfig: {
    logo: '/logo.svg',
    nav: [
      { text: 'Getting Started', link: '/getting-started/' },
      { text: 'Features', link: '/features/' },
      { text: 'Home Assistant', link: '/home-assistant/' },
      { text: 'Config', link: '/configuration/' },
      { text: 'Builder', link: '/builder' },
      { text: 'Developers', link: '/development/' },
    ],

    sidebar: [
      {
        text: 'Getting Started',
        items: [
          { text: 'What is njord?', link: '/getting-started/' },
          { text: 'Installation', link: '/getting-started/installation' },
          { text: 'Connect Home Assistant', link: '/getting-started/home-assistant' },
          { text: 'Features & Budget', link: '/getting-started/features' },
        ],
      },
      {
        text: 'Features',
        items: [
          { text: 'Overview', link: '/features/' },
          { text: 'Weather Alerts', link: '/features/weather-alerts' },
          { text: 'Air Quality', link: '/features/air-quality' },
          { text: 'Lifestyle', link: '/features/lifestyle' },
          { text: 'Energy', link: '/features/energy' },
          { text: 'Flood', link: '/features/flood' },
          { text: 'Marine', link: '/features/marine' },
          { text: 'Insights & Forecasts', link: '/features/insights' },
        ],
      },
      {
        text: 'Home Assistant',
        items: [
          { text: 'Installation', link: '/home-assistant/' },
          { text: 'Entities', link: '/home-assistant/entities' },
          { text: 'Options', link: '/home-assistant/options' },
          { text: 'Diagnostics', link: '/home-assistant/diagnostics' },
        ],
      },
      {
        text: 'Configuration',
        items: [
          { text: 'Overview', link: '/configuration/' },
          { text: 'Locations', link: '/configuration/locations' },
          { text: 'Forecast & Ensemble', link: '/configuration/weather' },
          { text: 'Model comparison', link: '/configuration/model-comparison' },
          { text: 'Enrichment', link: '/configuration/enrichment' },
          { text: 'Persistence', link: '/configuration/persistence' },
          { text: 'Budget', link: '/configuration/budget' },
          { text: 'Model Catalog', link: '/models' },
          { text: 'Config Builder', link: '/builder' },
        ],
      },
      {
        items: [
          { text: 'Attribution', link: '/attribution' },
        ],
      },
      {
        text: 'For developers',
        collapsed: true,
        items: [
          { text: 'Contributing', link: '/development/' },
          { text: 'Architecture', link: '/development/architecture' },
          { text: 'Repository layout', link: '/development/layout' },
          { text: 'Building & testing', link: '/development/building' },
          { text: 'Actors', link: '/development/actors' },
          { text: 'Data flow', link: '/development/data-flow' },
          { text: 'gRPC API', link: '/development/grpc' },
          { text: 'ha-njord integration', link: '/development/integration' },
          { text: 'Local development', link: '/development/local-dev' },
        ],
      },
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/st0o0/njord' },
    ],

    footer: {
      message: 'Weather data by <a href="https://open-meteo.com/">Open-Meteo.com</a>, licensed under <a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>. Data is enriched and transformed by njord.',
    },

    search: { provider: 'local' },
  },
}))
