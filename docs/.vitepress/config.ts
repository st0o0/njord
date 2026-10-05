import { defineConfig } from 'vitepress'
import { withLikeC4 } from '@leberkas-org/vitepress-likec4'

export default withLikeC4({ likec4: { source: './likec4', height: '460px' } }, defineConfig({
  base: '/',
  title: 'njord',
  description: 'Multi-model weather intelligence for Home Assistant',
  head: [['link', { rel: 'icon', type: 'image/svg+xml', href: '/logo.svg' }]],

  themeConfig: {
    logo: '/logo.svg',
    nav: [
      { text: 'Guide', link: '/getting-started' },
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
          { text: 'Models', link: '/configuration/models' },
          { text: 'Horizons', link: '/configuration/horizons' },
          { text: 'Parameters', link: '/configuration/parameters' },
          { text: 'Enrichment', link: '/configuration/enrichment' },
          { text: 'MQTT', link: '/configuration/mqtt' },
          { text: 'Persistence', link: '/configuration/persistence' },
          { text: 'Budget', link: '/configuration/budget' },
        ],
      },
      {
        text: 'Reference',
        items: [
          { text: 'Model Catalog', link: '/models' },
          { text: 'MQTT Topics', link: '/mqtt-reference' },
          { text: 'Config Builder', link: '/builder' },
        ],
      },
      {
        text: 'Development',
        items: [
          { text: 'Dev Setup', link: '/development/setup' },
          { text: 'Service (.NET)', link: '/development/service' },
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
