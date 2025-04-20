// filepath: c:\Users\clefw\HackerGame\v1\webpack.config.js
const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const TerserPlugin = require('terser-webpack-plugin');

// Configuration factory function that returns webpack config based on environment
module.exports = (env, argv) => {
  const isProduction = env && env.production === true;
  
  // Common configuration for both environments
  const config = {
    entry: './src/index.ts',
    module: {
      rules: [
        {
          test: /\.tsx?$/,
          use: 'ts-loader',
          exclude: /node_modules/,
        },
        {
          test: /\.css$/,
          use: ['style-loader', 'css-loader'],
        },
        {
          test: /\.(png|svg|jpg|jpeg|gif)$/i,
          type: 'asset/resource',
        },
      ],
    },
    resolve: {
      extensions: ['.tsx', '.ts', '.js'],
    },
    plugins: [
      new HtmlWebpackPlugin({
        title: 'Hacker Simulator',
        template: 'src/index.html'
      }),
    ],
  };
  if (isProduction) {
    // Production specific settings
    config.mode = 'production';
    config.devtool = false;
    config.output = {
      path: path.resolve(__dirname, 'dist'),
      filename: '[name].[contenthash].js',
    };
    config.optimization = {
      minimize: true,
      minimizer: [
        new TerserPlugin({
          test: /\.js(\?.*)?$/i,
          extractComments: false,
          terserOptions: {
            format: {
              comments: false,
            },
            compress: {
              drop_console: true,
            },
          },
          parallel: true,
        }),
      ],
      splitChunks: {
        chunks: 'all',
        cacheGroups: {
          vendor: {
            test: /[\\/]node_modules[\\/]/,
            name: 'vendors',
            chunks: 'all',
          },
        },
      },
    };} else {
    // Debug specific settings
    config.mode = 'development';
    config.devtool = 'eval-source-map';
    config.output = {
      path: path.resolve(__dirname, 'dist'),
      filename: '[name].js',
    };
    config.optimization = {
      minimize: false,
    };
    // Development server configuration
    config.devServer = {
      static: {
        directory: path.join(__dirname, 'dist'),
      },
      compress: true,
      port: 9000,
    };
  }

  return config;
};
