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
      { text: 'Guide', link: '/getting-started' },
      { text: 'Features', link: '/features/' },
      { text: 'Home Assistant', link: '/home-assistant/' },
      { text: 'Config', link: '/configuration/' },
      { text: 'Reference', link: '/models' },
      { text: 'Dev', link: '/development/setup' },
    ],

    sidebar: [
      {
        text: 'Guide',
        items: [
          { text: 'Getting Started', link: '/getting-started' },
          { text: 'Architecture', link: '/architecture' },
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
          { text: 'Weather', link: '/configuration/weather' },
          { text: 'Model comparison', link: '/configuration/model-comparison' },
          { text: 'Enrichment', link: '/configuration/enrichment' },
          { text: 'Persistence', link: '/configuration/persistence' },
          { text: 'Budget', link: '/configuration/budget' },
        ],
      },
      {
        text: 'Reference',
        items: [
          { text: 'Model Catalog', link: '/models' },
          { text: 'Config Builder', link: '/builder' },
        ],
      },
      {
        text: 'Development',
        items: [
          { text: 'Dev Setup', link: '/development/setup' },
          { text: 'Service (.NET)', link: '/development/service' },
          { text: 'Mutation Testing', link: '/development/mutation-testing' },
          { text: 'Integration (Python)', link: '/development/integration' },
          { text: 'Proto Management', link: '/development/protos' },
        ],
      },
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/st0o0/njord' },
    ],

    footer: {
      message: 'Open-Meteo data is licensed under CC BY 4.0.',
    },

    search: { provider: 'local' },
  },
}))
